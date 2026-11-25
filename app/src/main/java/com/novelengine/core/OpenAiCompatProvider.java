package com.novelengine.core;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.Charset;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * OpenAI 兼容协议的实现，同时覆盖两类真实端点：
 *  - 本地：llama.cpp server / Ollama / LM Studio（/v1/chat/completions）
 *  - API：任何 OpenAI 兼容云服务（带 Bearer 鉴权）
 */
public class OpenAiCompatProvider implements Provider {

    private final String endpoint;
    private final String model;
    private final String apiKey;
    private final int timeoutSec;
    private final boolean local;

    private static final Charset UTF8 = Charset.forName("UTF-8");

    public OpenAiCompatProvider(String baseUrl, String model, String apiKey, int timeoutSec, boolean local) {
        this.endpoint = normalize(baseUrl);
        this.model = model == null ? "" : model.trim();
        this.apiKey = apiKey == null ? "" : apiKey.trim();
        this.timeoutSec = Math.max(15, timeoutSec);
        this.local = local;
    }

    static String normalize(String baseUrl) {
        String u = baseUrl == null ? "" : baseUrl.trim();
        if (u.isEmpty()) throw new IllegalArgumentException("模型端点不能为空");
        while (u.endsWith("/")) u = u.substring(0, u.length() - 1);
        if (u.endsWith("/v1/chat/completions")) return u;
        if (u.endsWith("/chat/completions")) return u;
        if (u.endsWith("/v1")) return u + "/chat/completions";
        return u + "/v1/chat/completions";
    }

    public String endpoint() { return endpoint; }

    public String modelName() { return model; }

    @Override public String name() { return (local ? "local:" : "api:") + model; }

    @Override public boolean isLocal() { return local; }

    @Override
    public Result complete(String system, String user, int maxTokens, double temperature) throws Exception {
        Map<String, Object> body = new LinkedHashMap<String, Object>();
        body.put("model", model);
        List<Object> msgs = new ArrayList<Object>();
        if (system != null && !system.trim().isEmpty()) {
            Map<String, Object> s = new LinkedHashMap<String, Object>();
            s.put("role", "system");
            s.put("content", system);
            msgs.add(s);
        }
        Map<String, Object> u = new LinkedHashMap<String, Object>();
        u.put("role", "user");
        u.put("content", user);
        msgs.add(u);
        body.put("messages", msgs);
        body.put("temperature", Double.valueOf(temperature));
        body.put("max_tokens", Integer.valueOf(maxTokens));
        body.put("stream", Boolean.FALSE);

        String payload = Json.stringify(body);
        Exception last = null;
        for (int attempt = 0; attempt < 3; attempt++) {
            long t0 = System.currentTimeMillis();
            try {
                Reply r = post(payload);
                Result res = new Result();
                res.ms = System.currentTimeMillis() - t0;
                res.model = model;
                Map<String, Object> root = Json.obj(Json.parse(r.body));
                List<Object> choices = Json.arr(root.get("choices"));
                if (choices.isEmpty()) throw new ProviderException("响应缺少 choices：" + snippet(r.body), r.code);
                Map<String, Object> choice = Json.obj(choices.get(0));
                Map<String, Object> msg = Json.obj(choice.get("message"));
                res.text = Json.str(msg, "content", "");
                if (res.text == null || res.text.trim().isEmpty()) {
                    res.text = Json.str(choice, "text", "");
                }
                Map<String, Object> usage = Json.obj(root.get("usage"));
                res.tokensIn = Json.num(usage, "prompt_tokens", 0);
                res.tokensOut = Json.num(usage, "completion_tokens", 0);
                if (res.tokensIn == 0) res.tokensIn = TokenEstimator.estimate(system) + TokenEstimator.estimate(user);
                if (res.tokensOut == 0) res.tokensOut = TokenEstimator.estimate(res.text);
                return res;
            } catch (ProviderException pe) {
                last = pe;
                boolean retriable = pe.code == 429 || pe.code >= 500 || pe.code == -1;
                if (!retriable) throw pe;
                sleep(600L * (attempt + 1));
            } catch (IOException ioe) {
                last = ioe;
                sleep(600L * (attempt + 1));
            }
        }
        throw new ProviderException("请求模型失败（已重试 2 次）：" + (last == null ? "未知" : last.getMessage()), last);
    }

    /** 连通性探测：极小请求，返回一句人类可读结果。 */
    public String test() {
        long t0 = System.currentTimeMillis();
        try {
            Result r = complete("你是连通性探测助手，只回复一个词。", "ping", 8, 0.0);
            long ms = System.currentTimeMillis() - t0;
            String t = r.text == null ? "" : r.text.trim();
            if (t.length() > 40) t = t.substring(0, 40);
            return "可用 · " + ms + "ms · 模型回复：" + (t.isEmpty() ? "(空)" : t);
        } catch (Exception e) {
            return "不可用 · " + e.getMessage();
        }
    }

    private static final class Reply {
        final int code; final String body;
        Reply(int code, String body) { this.code = code; this.body = body; }
    }

    private Reply post(String payload) throws IOException, ProviderException {
        HttpURLConnection conn = null;
        try {
            URL url = new URL(endpoint);
            conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod("POST");
            conn.setConnectTimeout(Math.min(20000, timeoutSec * 1000));
            conn.setReadTimeout(timeoutSec * 1000);
            conn.setDoOutput(true);
            conn.setRequestProperty("Content-Type", "application/json; charset=utf-8");
            conn.setRequestProperty("Accept", "application/json");
            if (!apiKey.isEmpty()) conn.setRequestProperty("Authorization", "Bearer " + apiKey);
            byte[] bytes = payload.getBytes(UTF8);
            conn.setFixedLengthStreamingMode(bytes.length);
            OutputStream os = conn.getOutputStream();
            try {
                os.write(bytes);
                os.flush();
            } finally {
                os.close();
            }
            int code = conn.getResponseCode();
            String body;
            if (code >= 200 && code < 300) {
                body = read(conn.getInputStream());
            } else {
                body = read(conn.getErrorStream());
                throw new ProviderException("HTTP " + code + "：" + snippet(body), code);
            }
            return new Reply(code, body);
        } finally {
            if (conn != null) conn.disconnect();
        }
    }

    private static String read(InputStream in) throws IOException {
        if (in == null) return "";
        ByteArrayOutputStream bos = new ByteArrayOutputStream();
        byte[] buf = new byte[8192];
        int n;
        try {
            while ((n = in.read(buf)) > 0) bos.write(buf, 0, n);
        } finally {
            in.close();
        }
        return new String(bos.toByteArray(), UTF8);
    }

    private static String snippet(String s) {
        if (s == null) return "";
        String t = s.replace('\n', ' ').trim();
        return t.length() <= 240 ? t : t.substring(0, 240) + "…";
    }

    private static void sleep(long ms) {
        try { Thread.sleep(ms); } catch (InterruptedException ie) { Thread.currentThread().interrupt(); }
    }
}
