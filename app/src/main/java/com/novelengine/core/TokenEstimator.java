package com.novelengine.core;

/** 中日韩字符按 1 字≈0.68 token 粗估；其余按 4 字符≈1 token。仅用于成本预估与预算保护。 */
public final class TokenEstimator {

    private TokenEstimator() {}

    public static int estimate(String s) {
        if (s == null || s.isEmpty()) return 0;
        int cjk = 0, other = 0;
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (c >= 0x4E00 && c <= 0x9FFF) cjk++;
            else if (!Character.isWhitespace(c)) other++;
        }
        return (int) Math.ceil(cjk * 0.68 + other / 4.0);
    }
}
