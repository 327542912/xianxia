package com.novelengine.core;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** 极小 JSON 解析与序列化，纯 Java，无第三方依赖。仅支持标准 JSON。 */
public final class Json {

    private Json() {}

    public static Object parse(String s) {
        if (s == null) return null;
        Parser p = new Parser(s);
        p.ws();
        Object v = p.value();
        p.ws();
        if (p.i < p.n) throw new JsonException("多余内容于位置 " + p.i);
        return v;
    }

    public static String stringify(Object v) {
        StringBuilder sb = new StringBuilder();
        write(v, sb);
        return sb.toString();
    }

    @SuppressWarnings("unchecked")
    private static void write(Object v, StringBuilder sb) {
        if (v == null) { sb.append("null"); return; }
        if (v instanceof String) { writeString((String) v, sb); return; }
        if (v instanceof Boolean) { sb.append(v.toString()); return; }
        if (v instanceof Number) {
            double d = ((Number) v).doubleValue();
            if (d == Math.rint(d) && !Double.isInfinite(d)) sb.append(Long.toString((long) d));
            else sb.append(Double.toString(d));
            return;
        }
        if (v instanceof Map) {
            sb.append('{');
            boolean first = true;
            for (Map.Entry<Object, Object> e : ((Map<Object, Object>) v).entrySet()) {
                if (!first) sb.append(',');
                first = false;
                writeString(String.valueOf(e.getKey()), sb);
                sb.append(':');
                write(e.getValue(), sb);
            }
            sb.append('}');
            return;
        }
        if (v instanceof List) {
            sb.append('[');
            boolean first = true;
            for (Object o : (List<Object>) v) {
                if (!first) sb.append(',');
                first = false;
                write(o, sb);
            }
            sb.append(']');
            return;
        }
        writeString(String.valueOf(v), sb);
    }

    private static void writeString(String s, StringBuilder sb) {
        sb.append('"');
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            switch (c) {
                case '"': sb.append("\\\""); break;
                case '\\': sb.append("\\\\"); break;
                case '\n': sb.append("\\n"); break;
                case '\r': sb.append("\\r"); break;
                case '\t': sb.append("\\t"); break;
                default:
                    if (c < 0x20) sb.append(String.format("\\u%04x", (int) c));
                    else sb.append(c);
            }
        }
        sb.append('"');
    }

    // ---- 便捷取值 ----
    @SuppressWarnings("unchecked")
    public static Map<String, Object> obj(Object v) {
        if (v instanceof Map) return (Map<String, Object>) v;
        return new LinkedHashMap<String, Object>();
    }

    @SuppressWarnings("unchecked")
    public static List<Object> arr(Object v) {
        if (v instanceof List) return (List<Object>) v;
        return new ArrayList<Object>();
    }

    public static String str(Map<String, Object> m, String k, String def) {
        Object v = m.get(k);
        return v == null ? def : String.valueOf(v);
    }

    public static int num(Map<String, Object> m, String k, int def) {
        Object v = m.get(k);
        if (v instanceof Number) return ((Number) v).intValue();
        if (v instanceof String) {
            try { return Integer.parseInt(((String) v).trim()); } catch (Exception ignored) { }
        }
        return def;
    }

    public static boolean bool(Map<String, Object> m, String k, boolean def) {
        Object v = m.get(k);
        if (v instanceof Boolean) return ((Boolean) v).booleanValue();
        if (v instanceof String) return "true".equalsIgnoreCase((String) v);
        return def;
    }

    public static List<String> strList(Map<String, Object> m, String k) {
        List<String> out = new ArrayList<String>();
        for (Object o : arr(m.get(k))) out.add(String.valueOf(o));
        return out;
    }

    public static Map<String, Object> map(Object... kv) {
        Map<String, Object> m = new LinkedHashMap<String, Object>();
        for (int i = 0; i + 1 < kv.length; i += 2) m.put(String.valueOf(kv[i]), kv[i + 1]);
        return m;
    }

    public static String pretty(Object v) {
        StringBuilder sb = new StringBuilder();
        pretty(v, sb, 0);
        return sb.toString();
    }

    @SuppressWarnings("unchecked")
    private static void pretty(Object v, StringBuilder sb, int ind) {
        String pad = repeat("  ", ind);
        String pad2 = repeat("  ", ind + 1);
        if (v instanceof Map) {
            Map<Object, Object> m = (Map<Object, Object>) v;
            if (m.isEmpty()) { sb.append("{}"); return; }
            sb.append("{\n");
            boolean first = true;
            for (Map.Entry<Object, Object> e : m.entrySet()) {
                if (!first) sb.append(",\n");
                first = false;
                sb.append(pad2);
                writeString(String.valueOf(e.getKey()), sb);
                sb.append(": ");
                pretty(e.getValue(), sb, ind + 1);
            }
            sb.append('\n').append(pad).append('}');
            return;
        }
        if (v instanceof List) {
            List<Object> l = (List<Object>) v;
            if (l.isEmpty()) { sb.append("[]"); return; }
            sb.append("[\n");
            boolean first = true;
            for (Object o : l) {
                if (!first) sb.append(",\n");
                first = false;
                sb.append(pad2);
                pretty(o, sb, ind + 1);
            }
            sb.append('\n').append(pad).append(']');
            return;
        }
        write(v, sb);
    }

    private static String repeat(String s, int n) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < n; i++) sb.append(s);
        return sb.toString();
    }

    public static class JsonException extends RuntimeException {
        public JsonException(String m) { super(m); }
    }

    private static final class Parser {
        final String s; final int n; int i = 0;
        Parser(String s) { this.s = s; this.n = s.length(); }

        void ws() {
            while (i < n) {
                char c = s.charAt(i);
                if (c == ' ' || c == '\n' || c == '\r' || c == '\t') i++;
                else break;
            }
        }

        Object value() {
            if (i >= n) throw new JsonException("意外结束");
            char c = s.charAt(i);
            if (c == '{') return object();
            if (c == '[') return array();
            if (c == '"') return string();
            if (c == 't') { expect("true"); return Boolean.TRUE; }
            if (c == 'f') { expect("false"); return Boolean.FALSE; }
            if (c == 'n') { expect("null"); return null; }
            return number();
        }

        void expect(String lit) {
            if (i + lit.length() > n || !s.startsWith(lit, i)) throw new JsonException("期望 " + lit + " 于 " + i);
            i += lit.length();
        }

        Map<String, Object> object() {
            Map<String, Object> m = new LinkedHashMap<String, Object>();
            i++; ws();
            if (i < n && s.charAt(i) == '}') { i++; return m; }
            while (true) {
                ws();
                if (i >= n || s.charAt(i) != '"') throw new JsonException("键必须为字符串，位置 " + i);
                String k = string();
                ws();
                if (i >= n || s.charAt(i) != ':') throw new JsonException("缺少冒号，位置 " + i);
                i++; ws();
                m.put(k, value());
                ws();
                if (i >= n) throw new JsonException("对象未闭合");
                char c = s.charAt(i);
                if (c == ',') { i++; continue; }
                if (c == '}') { i++; return m; }
                throw new JsonException("对象内意外字符 " + c + " 于 " + i);
            }
        }

        List<Object> array() {
            List<Object> l = new ArrayList<Object>();
            i++; ws();
            if (i < n && s.charAt(i) == ']') { i++; return l; }
            while (true) {
                ws();
                l.add(value());
                ws();
                if (i >= n) throw new JsonException("数组未闭合");
                char c = s.charAt(i);
                if (c == ',') { i++; continue; }
                if (c == ']') { i++; return l; }
                throw new JsonException("数组内意外字符 " + c + " 于 " + i);
            }
        }

        String string() {
            i++;
            StringBuilder sb = new StringBuilder();
            while (i < n) {
                char c = s.charAt(i++);
                if (c == '"') return sb.toString();
                if (c == '\\') {
                    if (i >= n) throw new JsonException("转义未完成");
                    char e = s.charAt(i++);
                    switch (e) {
                        case '"': sb.append('"'); break;
                        case '\\': sb.append('\\'); break;
                        case '/': sb.append('/'); break;
                        case 'b': sb.append('\b'); break;
                        case 'f': sb.append('\f'); break;
                        case 'n': sb.append('\n'); break;
                        case 'r': sb.append('\r'); break;
                        case 't': sb.append('\t'); break;
                        case 'u':
                            if (i + 4 > n) throw new JsonException("\\u 不足四位");
                            sb.append((char) Integer.parseInt(s.substring(i, i + 4), 16));
                            i += 4;
                            break;
                        default: throw new JsonException("未知转义 \\" + e);
                    }
                } else {
                    sb.append(c);
                }
            }
            throw new JsonException("字符串未闭合");
        }

        Object number() {
            int st = i;
            if (i < n && (s.charAt(i) == '-' || s.charAt(i) == '+')) i++;
            while (i < n && "0123456789.eE+-".indexOf(s.charAt(i)) >= 0) i++;
            String t = s.substring(st, i);
            if (t.isEmpty()) throw new JsonException("非法数字于 " + st);
            try {
                if (t.indexOf('.') < 0 && t.indexOf('e') < 0 && t.indexOf('E') < 0) return Long.parseLong(t);
                return Double.parseDouble(t);
            } catch (NumberFormatException ex) {
                throw new JsonException("数字解析失败: " + t);
            }
        }
    }
}
