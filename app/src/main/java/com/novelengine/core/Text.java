package com.novelengine.core;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/** 文本度量工具。所有闸门的口径都从这里取，避免各处各算。 */
public final class Text {

    private Text() {}

    public static final String SENT_END = "。！？…；";

    public static List<String> sentences(String s) {
        List<String> out = new ArrayList<String>();
        if (s == null) return out;
        StringBuilder cur = new StringBuilder();
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (SENT_END.indexOf(c) >= 0) {
                String t = cur.toString().trim();
                if (!t.isEmpty()) out.add(t);
                cur.setLength(0);
            } else if (c == '\n') {
                String t = cur.toString().trim();
                if (!t.isEmpty()) out.add(t);
                cur.setLength(0);
            } else {
                cur.append(c);
            }
        }
        String t = cur.toString().trim();
        if (!t.isEmpty()) out.add(t);
        return out;
    }

    public static List<String> paragraphs(String s) {
        List<String> out = new ArrayList<String>();
        if (s == null) return out;
        String[] raw = s.split("\n");
        for (String p : raw) {
            String t = p.trim();
            if (!t.isEmpty()) out.add(t);
        }
        return out;
    }

    public static int percentile(List<Integer> values, double p) {
        if (values.isEmpty()) return 0;
        List<Integer> copy = new ArrayList<Integer>(values);
        java.util.Collections.sort(copy);
        int idx = (int) Math.round((copy.size() - 1) * p);
        if (idx < 0) idx = 0;
        if (idx >= copy.size()) idx = copy.size() - 1;
        return copy.get(idx).intValue();
    }

    public static List<Integer> sentenceLengths(String s) {
        List<Integer> out = new ArrayList<Integer>();
        for (String x : sentences(s)) out.add(Integer.valueOf(x.length()));
        return out;
    }

    /** 引号内字符占比（「」与“”两种引号体系都算）。 */
    public static double dialogueRatio(String s) {
        if (s == null || s.isEmpty()) return 0.0;
        int in = 0;
        boolean openA = false, openB = false;
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (c == '「') { openA = true; continue; }
            if (c == '」') { openA = false; continue; }
            if (c == '“') { openB = true; continue; }
            if (c == '”') { openB = false; continue; }
            if (openA || openB) in++;
        }
        return (double) in / (double) s.length();
    }

    public static int count(String s, String token) {
        if (s == null || token == null || token.isEmpty()) return 0;
        int c = 0, i = 0;
        while ((i = s.indexOf(token, i)) >= 0) { c++; i += token.length(); }
        return c;
    }

    public static Set<String> shingles(String s, int n) {
        Set<String> out = new HashSet<String>();
        if (s == null) return out;
        String t = s.replaceAll("\\s+", "");
        if (t.length() < n) return out;
        for (int i = 0; i + n <= t.length(); i++) out.add(t.substring(i, i + n));
        return out;
    }

    /** 只保留汉字，用于“纯汉字字数”口径。 */
    public static int hanCount(String s) {
        if (s == null) return 0;
        int c = 0;
        for (int i = 0; i < s.length(); i++) {
            char ch = s.charAt(i);
            if (ch >= 0x4E00 && ch <= 0x9FFF) c++;
        }
        return c;
    }

    public static String join(List<String> xs, String sep) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < xs.size(); i++) {
            if (i > 0) sb.append(sep);
            sb.append(xs.get(i));
        }
        return sb.toString();
    }
}
