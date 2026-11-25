package com.novelengine.core;

import com.novelengine.core.Models.HotfixEntry;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/** L7 热修合并与减重：同因合并、低效降级、超量退休，并给出体积增长率。 */
public final class HotfixBook {

    public static class Result {
        public List<HotfixEntry> kept = new ArrayList<HotfixEntry>();
        public int mergedIntoOne = 0;
        public int retired = 0;
        public double growthRatio = 0;
        public String note = "";
    }

    private HotfixBook() {}

    /** 合并规则：cause + 症状首 12 字相同者视为同一失败模式，保留最早一条，其余标 merged。 */
    public static Result compact(List<HotfixEntry> entries, int clauseChars, int versionChars) {
        Result r = new Result();
        Map<String, HotfixEntry> byKey = new HashMap<String, HotfixEntry>();
        for (HotfixEntry e : entries) {
            String symptom = e.symptom == null ? "" : e.symptom.trim();
            String head = symptom.length() > 12 ? symptom.substring(0, 12) : symptom;
            String key = e.cause + "|" + head;
            HotfixEntry first = byKey.get(key);
            if (first == null) {
                byKey.put(key, e);
                r.kept.add(e);
            } else {
                first.status = "merged";
                first.regression = (first.regression == null ? "" : first.regression) + " [并入 " + e.id + "]";
                r.mergedIntoOne++;
            }
        }
        // 减重：状态 retired 或回归为空的旧条目超量时退休
        List<HotfixEntry> finalList = new ArrayList<HotfixEntry>();
        int active = 0;
        for (HotfixEntry e : r.kept) {
            boolean stale = "retired".equals(e.status) || "merged".equals(e.status);
            if (!stale && active >= 20) {
                e.status = "retired";
                e.regression = (e.regression == null ? "" : e.regression) + " [超量退休]";
                r.retired++;
            }
            if (!("retired".equals(e.status) || "merged".equals(e.status))) active++;
            finalList.add(e);
        }
        r.kept = finalList;
        if (versionChars > 0) r.growthRatio = (double) clauseChars / (double) versionChars;
        r.note = "合并 " + r.mergedIntoOne + " 条，退休 " + r.retired + " 条，活跃 "
                + countActive(finalList) + " 条；条款体积占比 "
                + String.format(java.util.Locale.US, "%.1f%%", r.growthRatio * 100);
        return r;
    }

    private static int countActive(List<HotfixEntry> ls) {
        int n = 0;
        for (HotfixEntry e : ls) if (!("retired".equals(e.status) || "merged".equals(e.status))) n++;
        return n;
    }
}
