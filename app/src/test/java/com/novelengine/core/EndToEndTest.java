package com.novelengine.core;

import com.novelengine.core.Models.AppConfig;
import com.novelengine.core.Models.Chapter;
import com.novelengine.core.Models.Outline;
import com.novelengine.core.Models.OutlineChapter;
import com.novelengine.core.Models.StylePack;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;
import com.sun.net.httpserver.HttpServer;
import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.nio.charset.Charset;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Map;
import org.junit.AfterClass;
import org.junit.BeforeClass;
import org.junit.Test;
import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertTrue;

/**
 * 端到端：真 HTTP 服务端（OpenAI 兼容协议）→ Provider → 策划 → 10 章流水线 →
 * 四槽 → 四核对 → 卷末归并 → 热修合并 → 持久化。验证的是产品主链路，不是单个函数。
 */
public class EndToEndTest {

    private static HttpServer server;
    private static String baseUrl;
    private static final Charset UTF8 = Charset.forName("UTF-8");
    private static int plannerCalls = 0;
    private static int chapterCalls = 0;

    @BeforeClass public static void up() throws Exception {
        server = HttpServer.create(new InetSocketAddress("127.0.0.1", 0), 0);
        server.createContext("/v1/chat/completions", new HttpHandler() {
            @Override public void handle(HttpExchange ex) throws java.io.IOException {
                String reqBody = read(ex.getRequestBody());
                String user = userContent(reqBody);
                String content;
                if (user.contains("输出 JSON 结构")) {
                    plannerCalls++;
                    content = plannerJson();
                } else {
                    chapterCalls++;
                    content = chapterText(chapterNo(user));
                }
                String resp = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":"
                        + Json.stringify(content) + "}}],\"usage\":{\"prompt_tokens\":1200,\"completion_tokens\":900}}";
                byte[] out = resp.getBytes(UTF8);
                ex.getResponseHeaders().add("Content-Type", "application/json");
                ex.sendResponseHeaders(200, out.length);
                OutputStream os = ex.getResponseBody();
                os.write(out);
                os.close();
            }
        });
        server.start();
        baseUrl = "http://127.0.0.1:" + server.getAddress().getPort();
    }

    @AfterClass public static void down() {
        if (server != null) server.stop(0);
    }

    static String read(InputStream in) throws java.io.IOException {
        ByteArrayOutputStream bos = new ByteArrayOutputStream();
        byte[] buf = new byte[4096];
        int n;
        while ((n = in.read(buf)) > 0) bos.write(buf, 0, n);
        return new String(bos.toByteArray(), UTF8);
    }

    static String userContent(String body) {
        Map<String, Object> root = Json.obj(Json.parse(body));
        for (Object o : Json.arr(root.get("messages"))) {
            Map<String, Object> m = Json.obj(o);
            if ("user".equals(Json.str(m, "role", ""))) return Json.str(m, "content", "");
        }
        return "";
    }

    static int chapterNo(String user) {
        int p = user.indexOf("写第");
        if (p < 0) return 1;
        StringBuilder d = new StringBuilder();
        for (int i = p + 2; i < user.length(); i++) {
            char c = user.charAt(i);
            if (c >= '0' && c <= '9') d.append(c); else break;
        }
        return d.length() == 0 ? 1 : Integer.parseInt(d.toString());
    }

    static String plannerJson() {
        StringBuilder sb = new StringBuilder();
        sb.append("{\"logline\":\"一个人为了拿回欠条，把自己押进了矿场\",");
        sb.append("\"mainline\":\"起因：他替人担保欠下三百灵石，债主上门。升级：他签下招募契约进矿，用命抵账。转折：矿难让他发现欠条是伪造的。代价：他失去了唯一替他说话的人。悬念：契约期满那天，债主也进了矿。\",");
        sb.append("\"factions\":[\"矿场\",\"债主\",\"宗门外门\"],");
        sb.append("\"relations\":[");
        for (int i = 0; i < 5; i++) {
            if (i > 0) sb.append(',');
            sb.append("{\"a\":\"沈七\",\"b\":\"债主","").append(i).append("\",\"relation\":\"债与保\",\"tension\":\"谁先违约谁先死\"}");
        }
        sb.append("],\"hooks\":[");
        for (int i = 0; i < 6; i++) {
            if (i > 0) sb.append(',');
            sb.append("{\"name\":\"爽点").append(i).append("\",\"where\":\"第").append(i + 1).append("章\",\"payoff\":\"当场拿回一件东西\"}");
        }
        sb.append("],\"twists\":[");
        for (int i = 0; i < 4; i++) {
            if (i > 0) sb.append(',');
            sb.append("{\"name\":\"反转").append(i).append("\",\"where\":\"第").append(i + 1).append("章\",\"payoff\":\"第9章兑现\"}");
        }
        sb.append("],\"chapters\":[");
        for (int i = 1; i <= 10; i++) {
            if (i > 1) sb.append(',');
            sb.append("{\"no\":").append(i).append(",\"title\":\"第").append(i).append("章 对账\",")
              .append("\"stage\":\"推进\",\"coreEvent\":\"第").append(i).append("次对账翻出新的缺口\",")
              .append("\"cliffhanger\":\"账册最后一页写着他的名字，第").append(i).append("行被撕掉\",")
              .append("\"cast\":[\"沈七\"],\"threads\":[\"欠条第").append(i).append("页\"]}");
        }
        sb.append("]}");
        return sb.toString();
    }

    /** 章节文本：唯一化处理（每章数字不同，避免 20 字级复制），无“道”字（避开言说动词扫描），六字段账本在文末。 */
    static String chapterText(int no) {
        StringBuilder sb = new StringBuilder();
        for (int p = 0; p < 16; p++) {
            int a = no * 100 + p * 7 + 3;
            sb.append("第").append(a).append("日，沈七把").append(no * 10 + p)
              .append("号刀横在膝上，指腹抹过刀背，停在").append(a + 1).append("处缺口。");
            if (p % 3 == 0) {
                sb.append("「第").append(no).append("批货压在").append(no * 3 + p).append("号仓，钥匙不在我手上。」");
            }
            sb.append("\n\n");
        }
        sb.append("【跨章账本】第").append(no).append("章\n");
        sb.append("事件：沈七在第").append(no).append("次对账里翻出缺口\n");
        sb.append("物件：").append(no).append("号刀横在膝上，刃口第").append(no + 1).append("处有缺\n");
        sb.append("人物：沈七：石阶下，握刀\n");
        sb.append("时间：第").append(no).append("日戌时\n");
        sb.append("信息：他知道账册少了第").append(no).append("页\n");
        sb.append("线索：T").append(no).append("-1 欠条第").append(no).append("页");
        return sb.toString();
    }

    private static StylePack testPack() {
        StylePack p = new StylePack();
        p.id = "test";
        p.name = "测试风格";
        p.params.put("p50", new double[]{8, 80});
        p.params.put("p90max", new double[]{40, 140});
        p.params.put("dialogue", new double[]{0.02, 0.95});
        p.banned = new ArrayList<String>();
        return p;
    }

    @Test public void fullPipelineRunsTenChapters() throws Exception {
        OpenAiCompatProvider provider = new OpenAiCompatProvider(baseUrl, "stub-model", "test-key", 30, true);
        assertTrue("连通性", provider.test().startsWith("可用"));

        AppConfig cfg = new AppConfig();
        cfg.title = "矿场账";
        cfg.genre = "古典仙侠";
        cfg.hero = "沈七：外门矿工，口拙，认死理";
        cfg.conflict = "他要拿回欠条，债主要他拿命抵";
        cfg.targetChars = chapterText(1).length() - 120;
        StylePack pack = testPack();

        Outline outline = new Planner(provider).plan(cfg, pack);
        assertEquals(10, outline.chapters.size());
        assertTrue(Planner.validate(outline).isEmpty());

        InMemoryRepository repo = new InMemoryRepository();
        repo.saveConfig("p1", cfg);
        repo.saveOutline("p1", outline);

        List<Chapter> delivered = new ArrayList<Chapter>();
        List<String> history = new ArrayList<String>();
        for (int i = 1; i <= 10; i++) {
            OutlineChapter plan = outline.chapter(i);
            Pipeline.Request r = new Pipeline.Request();
            r.cfg = cfg;
            r.pack = pack;
            r.clauses = Defaults.select(Defaults.clausesC(), Defaults.clausesM(), Defaults.clausesG(), cfg.genre, plan.stage);
            r.plan = plan;
            r.prev = i > 1 ? delivered.get(delivered.size() - 1) : null;
            r.lastLedger = r.prev == null ? null : r.prev.ledger;
            r.knownNames = new ArrayList<String>(Arrays.asList("沈七"));
            r.historyBodies = history;
            r.threads = new ArrayList<ThreadItem>();
            List<String[]> chars = new ArrayList<String[]>();
            chars.add(new String[]{"沈七", "石阶下", "在场", String.valueOf(i)});
            r.slots = Slots.render(Slots.fromProgress(gameLine(outline), plan.stage, 10 - i + 1,
                    plan.cliffhanger, chars, new ArrayList<String[]>(), new ArrayList<String>()));
            r.injectionForEcho = pack.injection + "\n" + r.slots;
            r.runJudge = false;
            r.maxRewrites = 2;
            r.meter = new CostMeter();

            Chapter ch = new Pipeline(provider).generate(r);
            repo.saveChapter("p1", ch);
            assertNotNull("账本必须解析成功（第" + i + "章）", ch.ledger);
            assertEquals("第" + i + "章应交付", "delivered", ch.status);
            assertTrue("槽位不超 40 行", Slots.lineCount(r.slots) <= Slots.MAX_TOTAL_LINES);
            history.add(ch.text);
            delivered.add(ch);
        }
        assertEquals(10, delivered.size());
        assertEquals(10, repo.loadChapters("p1").size());

        // 四核对
        CrossCheck.Input in = new CrossCheck.Input();
        in.chapter = delivered.get(0);
        List<CrossCheck.Item> items = CrossCheck.run(in);
        assertEquals(4, items.size());

        // 卷末归并
        VolumeMerge.Result merge = VolumeMerge.run(1, delivered, new ArrayList<ThreadItem>());
        assertFalse(merge.volumeSummary.isEmpty());

        // 热修合并
        List<Models.HotfixEntry> entries = new ArrayList<Models.HotfixEntry>();
        for (int i = 0; i < 3; i++) {
            Models.HotfixEntry e = new Models.HotfixEntry();
            e.id = "HF-" + i;
            e.cause = "missing";
            e.symptom = "第1段 6 句，超过上限";
            e.status = "active";
            entries.add(e);
        }
        HotfixBook.Result compact = HotfixBook.compact(entries, 800, 6000);
        assertEquals("同因同症状应合并为一条", 1, compact.kept.size());
        assertEquals(2, compact.mergedIntoOne);
    }

    @Test public void runModeContinuousTenChapters() throws Exception {
        final OpenAiCompatProvider provider = new OpenAiCompatProvider(baseUrl, "stub-model", "", 30, true);
        final StylePack pack = testPack();
        final AppConfig cfg = new AppConfig();
        cfg.title = "连跑";
        cfg.genre = "古典仙侠";
        cfg.targetChars = chapterText(1).length() - 120;
        final InMemoryRepository repo = new InMemoryRepository();
        RunMode.Summary s = RunMode.run(1, 10, new RunMode.ChapterWriter() {
            @Override public Chapter write(int no) throws Exception {
                OutlineChapter plan = new OutlineChapter();
                plan.no = no;
                plan.title = "第" + no + "章";
                plan.stage = "推进";
                plan.coreEvent = "对账";
                plan.cliffhanger = "账册少了一页";
                plan.cast = new ArrayList<String>(Arrays.asList("沈七"));
                Pipeline.Request r = new Pipeline.Request();
                r.cfg = cfg;
                r.pack = pack;
                r.clauses = new ArrayList<Models.Clause>();
                r.plan = plan;
                r.knownNames = new ArrayList<String>(Arrays.asList("沈七"));
                r.historyBodies = new ArrayList<String>();
                r.threads = new ArrayList<ThreadItem>();
                r.runJudge = false;
                Chapter ch = new Pipeline(provider).generate(r);
                repo.saveChapter("p2", ch);
                return ch;
            }
        }, null);
        assertEquals(10, s.requested);
        assertEquals("A 模式应交付全部 10 章", 10, s.delivered);
        assertTrue(s.complete());
        assertEquals(10, repo.loadChapters("p2").size());
        assertTrue("策划至少被调用一次", plannerCalls >= 1);
    }

    private static String gameLine(Outline o) {
        String m = o.mainline;
        return m.length() > 40 ? m.substring(0, 40) : m;
    }
}
