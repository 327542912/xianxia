package com.novelengine.core;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** 领域模型。全部纯 Java，不含 Android 依赖，便于 JVM 单测。 */
public final class Models {

    private Models() {}

    /** L0 配置锁 */
    public static class AppConfig {
        public String title = "";
        public String genre = "";
        public String platform = "";
        public String hero = "";
        public String conflict = "";
        public String world = "";
        public String resource = "";
        public int targetChars = 2400;
        public String stylePackId = "xianxia-baimiao";
        public String modelTier = "local";
        public boolean locked = false;
        public long lockedAt = 0L;

        public void validate() {
            if (isBlank(title)) throw new IllegalStateException("作品名不能为空");
            if (isBlank(genre)) throw new IllegalStateException("题材不能为空");
            if (isBlank(hero)) throw new IllegalStateException("主角人设不能为空");
            if (isBlank(conflict)) throw new IllegalStateException("核心冲突不能为空");
            if (targetChars < 800 || targetChars > 6000) throw new IllegalStateException("单章字数应在 800–6000");
        }

        public Map<String, Object> toJson() {
            Map<String, Object> m = new LinkedHashMap<String, Object>();
            m.put("title", title); m.put("genre", genre); m.put("platform", platform);
            m.put("hero", hero); m.put("conflict", conflict); m.put("world", world);
            m.put("resource", resource); m.put("targetChars", Integer.valueOf(targetChars));
            m.put("stylePackId", stylePackId); m.put("modelTier", modelTier);
            m.put("locked", Boolean.valueOf(locked)); m.put("lockedAt", Long.valueOf(lockedAt));
            return m;
        }

        public static AppConfig fromJson(Map<String, Object> m) {
            AppConfig c = new AppConfig();
            c.title = Json.str(m, "title", "");
            c.genre = Json.str(m, "genre", "");
            c.platform = Json.str(m, "platform", "");
            c.hero = Json.str(m, "hero", "");
            c.conflict = Json.str(m, "conflict", "");
            c.world = Json.str(m, "world", "");
            c.resource = Json.str(m, "resource", "");
            c.targetChars = Json.num(m, "targetChars", 2400);
            c.stylePackId = Json.str(m, "stylePackId", "xianxia-baimiao");
            c.modelTier = Json.str(m, "modelTier", "local");
            c.locked = Json.bool(m, "locked", false);
            c.lockedAt = (long) Json.num(m, "lockedAt", 0);
            return c;
        }

        static boolean isBlank(String s) { return s == null || s.trim().isEmpty(); }
    }

    /** 模型接入设置 */
    public static class ProviderSettings {
        public String mode = "local";              // local | api
        public String localBaseUrl = "http://127.0.0.1:8080";
        public String localModel = "qwen2.5-7b-instruct";
        public String apiBaseUrl = "https://api.deepseek.com";
        public String apiModel = "deepseek-chat";
        public String apiKey = "";
        public int timeoutSec = 120;
        public int maxTokens = 4096;
        public double temperature = 0.85;

        public Map<String, Object> toJson() {
            Map<String, Object> m = new LinkedHashMap<String, Object>();
            m.put("mode", mode); m.put("localBaseUrl", localBaseUrl); m.put("localModel", localModel);
            m.put("apiBaseUrl", apiBaseUrl); m.put("apiModel", apiModel);
            m.put("timeoutSec", Integer.valueOf(timeoutSec));
            m.put("maxTokens", Integer.valueOf(maxTokens));
            m.put("temperature", Double.valueOf(temperature));
            return m;
        }

        public static ProviderSettings fromJson(Map<String, Object> m) {
            ProviderSettings p = new ProviderSettings();
            p.mode = Json.str(m, "mode", "local");
            p.localBaseUrl = Json.str(m, "localBaseUrl", "http://127.0.0.1:8080");
            p.localModel = Json.str(m, "localModel", "qwen2.5-7b-instruct");
            p.apiBaseUrl = Json.str(m, "apiBaseUrl", "https://api.deepseek.com");
            p.apiModel = Json.str(m, "apiModel", "deepseek-chat");
            p.timeoutSec = Json.num(m, "timeoutSec", 120);
            p.maxTokens = Json.num(m, "maxTokens", 4096);
            Object t = m.get("temperature");
            p.temperature = t instanceof Number ? ((Number) t).doubleValue() : 0.85;
            return p;
        }
    }

    /** L9 风格包 */
    public static class StylePack {
        public String id = "";
        public String name = "";
        public String genre = "";
        public String positioning = "";
        public String injection = "";                 // ≤2500 字
        public List<String> banned = new ArrayList<String>();
        public Map<String, double[]> params = new LinkedHashMap<String, double[]>(); // 名称 -> {min,max}
        public List<String> examples = new ArrayList<String>();

        public double[] range(String key, double dmin, double dmax) {
            double[] r = params.get(key);
            if (r == null || r.length < 2) return new double[]{dmin, dmax};
            return r;
        }

        public Map<String, Object> toJson() {
            Map<String, Object> m = new LinkedHashMap<String, Object>();
            m.put("id", id); m.put("name", name); m.put("genre", genre);
            m.put("positioning", positioning); m.put("injection", injection);
            m.put("banned", new ArrayList<Object>(banned));
            m.put("examples", new ArrayList<Object>(examples));
            return m;
        }

        public static StylePack fromJson(Map<String, Object> m) {
            StylePack s = new StylePack();
            s.id = Json.str(m, "id", "");
            s.name = Json.str(m, "name", "");
            s.genre = Json.str(m, "genre", "");
            s.positioning = Json.str(m, "positioning", "");
            s.injection = Json.str(m, "injection", "");
            s.banned = Json.strList(m, "banned");
            s.examples = Json.strList(m, "examples");
            return s;
        }
    }

    /** L2 条款 */
    public static class Clause {
        public String id = "";
        public String name = "";
        public String type = "hard";     // hard | range | mode | style
        public String judge = "code";    // code | model
        public String rule = "";
        public String load = "C";        // C | M | G
        public String onFail = "rewrite";

        public Clause() {}

        public Clause(String id, String name, String type, String judge, String rule, String load, String onFail) {
            this.id = id; this.name = name; this.type = type; this.judge = judge;
            this.rule = rule; this.load = load; this.onFail = onFail;
        }

        public Map<String, Object> toJson() {
            Map<String, Object> m = new LinkedHashMap<String, Object>();
            m.put("id", id); m.put("name", name); m.put("type", type);
            m.put("judge", judge); m.put("rule", rule); m.put("load", load); m.put("onFail", onFail);
            return m;
        }

        public static Clause fromJson(Map<String, Object> m) {
            return new Clause(Json.str(m, "id", ""), Json.str(m, "name", ""), Json.str(m, "type", "hard"),
                    Json.str(m, "judge", "code"), Json.str(m, "rule", ""), Json.str(m, "load", "C"),
                    Json.str(m, "onFail", "rewrite"));
        }
    }

    /** 关系条目 */
    public static class Relation {
        public String a = "";
        public String b = "";
        public String relation = "";
        public String tension = "";
    }

    /** 爽点 / 反转 */
    public static class Beat {
        public String name = "";
        public String where = "";
        public String payoff = "";
    }

    /** 章节大纲 */
    public static class OutlineChapter {
        public int no = 1;
        public String title = "";
        public String coreEvent = "";
        public String cliffhanger = "";
        public String stage = "";
        public List<String> cast = new ArrayList<String>();
        public List<String> threads = new ArrayList<String>();
    }

    /** 全书策划 */
    public static class Outline {
        public String logline = "";
        public String mainline = "";
        public List<String> factions = new ArrayList<String>();
        public List<Relation> relations = new ArrayList<Relation>();
        public List<Beat> hooks = new ArrayList<Beat>();      // 爽点
        public List<Beat> twists = new ArrayList<Beat>();     // 反转与伏笔
        public List<OutlineChapter> chapters = new ArrayList<OutlineChapter>();

        public OutlineChapter chapter(int no) {
            for (OutlineChapter c : chapters) if (c.no == no) return c;
            return null;
        }

        public Map<String, Object> toJson() {
            Map<String, Object> m = new LinkedHashMap<String, Object>();
            m.put("logline", logline); m.put("mainline", mainline);
            List<Object> f = new ArrayList<Object>();
            for (String s : factions) f.add(s);
            m.put("factions", f);
            List<Object> rel = new ArrayList<Object>();
            for (Relation r : relations) {
                Map<String, Object> x = new LinkedHashMap<String, Object>();
                x.put("a", r.a); x.put("b", r.b); x.put("relation", r.relation); x.put("tension", r.tension);
                rel.add(x);
            }
            m.put("relations", rel);
            m.put("hooks", beatList(hooks));
            m.put("twists", beatList(twists));
            List<Object> chs = new ArrayList<Object>();
            for (OutlineChapter c : chapters) {
                Map<String, Object> x = new LinkedHashMap<String, Object>();
                x.put("no", Integer.valueOf(c.no)); x.put("title", c.title);
                x.put("coreEvent", c.coreEvent); x.put("cliffhanger", c.cliffhanger);
                x.put("stage", c.stage);
                List<Object> cast = new ArrayList<Object>();
                for (String s : c.cast) cast.add(s);
                x.put("cast", cast);
                x.put("threads", new ArrayList<Object>(c.threads));
                chs.add(x);
            }
            m.put("chapters", chs);
            return m;
        }

        private static List<Object> beatList(List<Beat> bs) {
            List<Object> out = new ArrayList<Object>();
            for (Beat b : bs) {
                Map<String, Object> x = new LinkedHashMap<String, Object>();
                x.put("name", b.name); x.put("where", b.where); x.put("payoff", b.payoff);
                out.add(x);
            }
            return out;
        }

        public static Outline fromJson(Map<String, Object> m) {
            Outline o = new Outline();
            o.logline = Json.str(m, "logline", "");
            o.mainline = Json.str(m, "mainline", "");
            o.factions = Json.strList(m, "factions");
            for (Object ro : Json.arr(m.get("relations"))) {
                Map<String, Object> r = Json.obj(ro);
                Relation rel = new Relation();
                rel.a = Json.str(r, "a", ""); rel.b = Json.str(r, "b", "");
                rel.relation = Json.str(r, "relation", ""); rel.tension = Json.str(r, "tension", "");
                o.relations.add(rel);
            }
            readBeats(m, "hooks", o.hooks);
            readBeats(m, "twists", o.twists);
            for (Object co : Json.arr(m.get("chapters"))) {
                Map<String, Object> c = Json.obj(co);
                OutlineChapter oc = new OutlineChapter();
                oc.no = Json.num(c, "no", o.chapters.size() + 1);
                oc.title = Json.str(c, "title", "");
                oc.coreEvent = Json.str(c, "coreEvent", "");
                oc.cliffhanger = Json.str(c, "cliffhanger", "");
                oc.stage = Json.str(c, "stage", "");
                oc.cast = Json.strList(c, "cast");
                oc.threads = Json.strList(c, "threads");
                o.chapters.add(oc);
            }
            return o;
        }

        private static void readBeats(Map<String, Object> m, String key, List<Beat> out) {
            for (Object bo : Json.arr(m.get(key))) {
                Map<String, Object> b = Json.obj(bo);
                Beat beat = new Beat();
                beat.name = Json.str(b, "name", "");
                beat.where = Json.str(b, "where", "");
                beat.payoff = Json.str(b, "payoff", "");
                out.add(beat);
            }
        }
    }

    /** L4 六字段账本 */
    public static class Ledger {
        public int chapter = 0;
        public String event = "";
        public String item = "";
        public String person = "";
        public String time = "";
        public String info = "";
        public String thread = "";

        public boolean complete() {
            return !isBlank(event) && !isBlank(item) && !isBlank(person)
                    && !isBlank(time) && !isBlank(info) && !isBlank(thread)
                    && chapter > 0;
        }

        public String render() {
            StringBuilder sb = new StringBuilder();
            sb.append("【跨章账本】第").append(chapter).append("章\n");
            sb.append("事件：").append(nz(event)).append('\n');
            sb.append("物件：").append(nz(item)).append('\n');
            sb.append("人物：").append(nz(person)).append('\n');
            sb.append("时间：").append(nz(time)).append('\n');
            sb.append("信息：").append(nz(info)).append('\n');
            sb.append("线索：").append(nz(thread));
            return sb.toString();
        }

        public Map<String, Object> toJson() {
            Map<String, Object> m = new LinkedHashMap<String, Object>();
            m.put("chapter", Integer.valueOf(chapter));
            m.put("event", event); m.put("item", item); m.put("person", person);
            m.put("time", time); m.put("info", info); m.put("thread", thread);
            return m;
        }

        public static Ledger fromJson(Map<String, Object> m) {
            Ledger l = new Ledger();
            l.chapter = Json.num(m, "chapter", 0);
            l.event = Json.str(m, "event", ""); l.item = Json.str(m, "item", "");
            l.person = Json.str(m, "person", ""); l.time = Json.str(m, "time", "");
            l.info = Json.str(m, "info", ""); l.thread = Json.str(m, "thread", "");
            return l;
        }

        static boolean isBlank(String s) { return s == null || s.trim().isEmpty(); }
        static String nz(String s) { return s == null ? "" : s; }
    }

    /** 闸门结果 */
    public static class GateResult {
        public String id = "";
        public String name = "";
        public boolean pass = true;
        public String detail = "";
        public boolean code = true;

        public GateResult() {}

        public GateResult(String id, String name, boolean pass, String detail, boolean code) {
            this.id = id; this.name = name; this.pass = pass; this.detail = detail; this.code = code;
        }
    }

    /** 模型判定条目 */
    public static class JudgeVerdict {
        public String id = "";
        public String name = "";
        public String status = "ok";   // ok | warn | bad
        public String note = "";
        public String evidence = "";
    }

    /** 章 */
    public static class Chapter {
        public int no = 1;
        public String title = "";
        public String text = "";
        public Ledger ledger;
        public List<GateResult> gates = new ArrayList<GateResult>();
        public List<JudgeVerdict> verdicts = new ArrayList<JudgeVerdict>();
        public int tokensIn = 0;
        public int tokensOut = 0;
        public long ms = 0;
        public int rewrites = 0;
        public String status = "draft";  // draft | gate_failed | delivered | held
        public String provider = "";

        public boolean gatesPass() {
            for (GateResult g : gates) if (!g.pass) return false;
            return true;
        }

        public List<GateResult> failedGates() {
            List<GateResult> out = new ArrayList<GateResult>();
            for (GateResult g : gates) if (!g.pass) out.add(g);
            return out;
        }

        public int charsWithPunct() { return text == null ? 0 : text.length(); }

        public Map<String, Object> toJson() {
            Map<String, Object> m = new LinkedHashMap<String, Object>();
            m.put("no", Integer.valueOf(no)); m.put("title", title); m.put("text", text);
            m.put("status", status); m.put("rewrites", Integer.valueOf(rewrites));
            m.put("tokensIn", Integer.valueOf(tokensIn)); m.put("tokensOut", Integer.valueOf(tokensOut));
            m.put("ms", Long.valueOf(ms)); m.put("provider", provider);
            m.put("ledger", ledger == null ? null : ledger.toJson());
            List<Object> gs = new ArrayList<Object>();
            for (GateResult g : gates) {
                Map<String, Object> x = new LinkedHashMap<String, Object>();
                x.put("id", g.id); x.put("name", g.name);
                x.put("pass", Boolean.valueOf(g.pass)); x.put("detail", g.detail);
                x.put("code", Boolean.valueOf(g.code));
                gs.add(x);
            }
            m.put("gates", gs);
            List<Object> vs = new ArrayList<Object>();
            for (JudgeVerdict v : verdicts) {
                Map<String, Object> x = new LinkedHashMap<String, Object>();
                x.put("id", v.id); x.put("name", v.name); x.put("status", v.status);
                x.put("note", v.note); x.put("evidence", v.evidence);
                vs.add(x);
            }
            m.put("verdicts", vs);
            return m;
        }

        public static Chapter fromJson(Map<String, Object> m) {
            Chapter c = new Chapter();
            c.no = Json.num(m, "no", 1);
            c.title = Json.str(m, "title", "");
            c.text = Json.str(m, "text", "");
            c.status = Json.str(m, "status", "draft");
            c.rewrites = Json.num(m, "rewrites", 0);
            c.tokensIn = Json.num(m, "tokensIn", 0);
            c.tokensOut = Json.num(m, "tokensOut", 0);
            c.ms = (long) Json.num(m, "ms", 0);
            c.provider = Json.str(m, "provider", "");
            Object l = m.get("ledger");
            if (l instanceof Map) c.ledger = Ledger.fromJson(Json.obj(l));
            for (Object go : Json.arr(m.get("gates"))) {
                Map<String, Object> g = Json.obj(go);
                c.gates.add(new GateResult(Json.str(g, "id", ""), Json.str(g, "name", ""),
                        Json.bool(g, "pass", true), Json.str(g, "detail", ""), Json.bool(g, "code", true)));
            }
            for (Object vo : Json.arr(m.get("verdicts"))) {
                Map<String, Object> v = Json.obj(vo);
                JudgeVerdict j = new JudgeVerdict();
                j.id = Json.str(v, "id", ""); j.name = Json.str(v, "name", "");
                j.status = Json.str(v, "status", "ok"); j.note = Json.str(v, "note", "");
                j.evidence = Json.str(v, "evidence", "");
                c.verdicts.add(j);
            }
            return c;
        }
    }

    /** L7 热修台账 */
    public static class HotfixEntry {
        public String id = "";
        public long date = 0L;
        public String sample = "";
        public String symptom = "";
        public String cause = "";       // missing | ambiguous | misplaced | capability
        public String action = "";      // NEW-Sxx / MODIFY-Sxx / RETIRE-Sxx
        public String layer = "C";
        public String regression = "";
        public String status = "active"; // active | retired | merged

        public Map<String, Object> toJson() {
            Map<String, Object> m = new LinkedHashMap<String, Object>();
            m.put("id", id); m.put("date", Long.valueOf(date)); m.put("sample", sample);
            m.put("symptom", symptom); m.put("cause", cause); m.put("action", action);
            m.put("layer", layer); m.put("regression", regression); m.put("status", status);
            return m;
        }

        public static HotfixEntry fromJson(Map<String, Object> m) {
            HotfixEntry e = new HotfixEntry();
            e.id = Json.str(m, "id", "");
            e.date = (long) Json.num(m, "date", 0);
            e.sample = Json.str(m, "sample", "");
            e.symptom = Json.str(m, "symptom", "");
            e.cause = Json.str(m, "cause", "missing");
            e.action = Json.str(m, "action", "");
            e.layer = Json.str(m, "layer", "C");
            e.regression = Json.str(m, "regression", "");
            e.status = Json.str(m, "status", "active");
            return e;
        }
    }
}
