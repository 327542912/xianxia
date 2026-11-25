package com.novelengine.core;

import com.novelengine.core.Models.AppConfig;
import com.novelengine.core.Models.Outline;
import com.novelengine.core.Models.OutlineChapter;
import com.novelengine.core.Models.Relation;
import com.novelengine.core.Models.StylePack;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

/** 策划：把三要素扩成主线 + 关系 + 爽点 + 反转 + 10 章大纲；带一次结构修复。 */
public class Planner {

    public static class PlannerException extends Exception {
        public final String raw;
        public PlannerException(String message, String raw) { super(message); this.raw = raw; }
    }

    private final Provider provider;

    public Planner(Provider provider) { this.provider = provider; }

    public Outline plan(AppConfig cfg, StylePack pack) throws Exception {
        String user = Prompts.plannerUser(cfg, pack);
        Provider.Result r = provider.complete(Prompts.PLANNER_SYSTEM, user, 4096, 0.9);
        String json = extractJson(r.text);
        Outline outline;
        List<String> problems;
        try {
            outline = Outline.fromJson(Json.obj(Json.parse(json)));
            problems = validate(outline);
        } catch (Exception e) {
            problems = new ArrayList<String>();
            problems.add("JSON 无法解析：" + e.getMessage());
            outline = null;
        }
        if (problems.isEmpty() && outline != null) return outline;

        Provider.Result r2 = provider.complete(Prompts.PLANNER_SYSTEM,
                Prompts.plannerRepair(json, problems), 4096, 0.7);
        String json2 = extractJson(r2.text);
        try {
            Outline fixed = Outline.fromJson(Json.obj(Json.parse(json2)));
            List<String> p2 = validate(fixed);
            if (p2.isEmpty()) return fixed;
            throw new PlannerException("策划未通过校验：" + join(p2), json2);
        } catch (PlannerException pe) {
            throw pe;
        } catch (Exception e) {
            throw new PlannerException("修复后仍无法解析：" + e.getMessage(), json2);
        }
    }

    /** 结构校验：只看可判定的东西，不评价好坏。 */
    public static List<String> validate(Outline o) {
        List<String> p = new ArrayList<String>();
        if (o.logline == null || o.logline.trim().isEmpty()) p.add("缺少 logline");
        if (o.mainline == null || o.mainline.trim().length() < 60) p.add("mainline 太短（应 150-260 字，写明起因→升级→转折→代价→悬念）");
        if (o.relations.size() < 5) p.add("relations 至少 5 条，当前 " + o.relations.size());
        for (Relation r : o.relations) {
            if (blank(r.a) || blank(r.b) || blank(r.relation)) { p.add("relations 中存在缺少 a/b/relation 的条目"); break; }
        }
        if (o.hooks.size() < 6) p.add("hooks 至少 6 条，当前 " + o.hooks.size());
        if (o.twists.size() < 4) p.add("twists 至少 4 条，当前 " + o.twists.size());
        if (o.chapters.size() != 10) p.add("chapters 必须正好 10 条，当前 " + o.chapters.size());
        Set<Integer> seen = new HashSet<Integer>();
        for (int i = 0; i < o.chapters.size(); i++) {
            OutlineChapter c = o.chapters.get(i);
            int expect = i + 1;
            if (c.no != expect) p.add("第 " + expect + " 条章号应为 " + expect + "，实际 " + c.no);
            if (!seen.add(Integer.valueOf(c.no))) p.add("章号重复：" + c.no);
            if (blank(c.title)) p.add("第 " + c.no + " 章缺少标题");
            if (blank(c.coreEvent)) p.add("第 " + c.no + " 章缺少 coreEvent");
            if (blank(c.cliffhanger) || c.cliffhanger.length() < 6) p.add("第 " + c.no + " 章 cliffhanger 太短或缺失");
        }
        return p;
    }

    static String extractJson(String text) {
        if (text == null) throw new Json.JsonException("模型未返回内容");
        int s = text.indexOf('{');
        int e = text.lastIndexOf('}');
        if (s < 0 || e <= s) throw new Json.JsonException("未找到 JSON 对象");
        return text.substring(s, e + 1);
    }

    private static boolean blank(String s) { return s == null || s.trim().isEmpty(); }

    private static String join(List<String> xs) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < xs.size(); i++) {
            if (i > 0) sb.append("；");
            sb.append(xs.get(i));
        }
        return sb.toString();
    }
}
