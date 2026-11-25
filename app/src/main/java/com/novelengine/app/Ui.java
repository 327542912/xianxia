package com.novelengine.app;

import android.content.Context;
import android.graphics.Color;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.TextView;

/** 视图构造小工具：所有行都是真实原生控件，不用 WebView。 */
public final class Ui {

    private Ui() {}

    public static int dp(Context c, int v) {
        return (int) (v * c.getResources().getDisplayMetrics().density + 0.5f);
    }

    public static TextView text(Context c, String s, int sizeSp, int color) {
        TextView t = new TextView(c);
        t.setText(s == null ? "" : s);
        t.setTextSize(sizeSp);
        t.setTextColor(color);
        t.setLineSpacing(dp(c, 2), 1.35f);
        t.setPadding(0, dp(c, 4), 0, dp(c, 4));
        return t;
    }

    public static TextView section(Context c, String s) {
        TextView t = text(c, s, 12, Color.parseColor("#5C6470"));
        t.setLetterSpacing(0.08f);
        t.setPadding(0, dp(c, 14), 0, dp(c, 4));
        return t;
    }

    public static TextView mono(Context c, String s) {
        TextView t = text(c, s, 12, Color.parseColor("#1B1D21"));
        t.setTypeface(android.graphics.Typeface.MONOSPACE);
        t.setBackgroundResource(R.drawable.bg_card);
        t.setPadding(dp(c, 12), dp(c, 10), dp(c, 12), dp(c, 10));
        return t;
    }

    public static TextView chip(Context c, String s, boolean ok) {
        TextView t = text(c, s, 12, ok ? Color.parseColor("#1F7A4D") : Color.parseColor("#A33B2C"));
        t.setBackgroundResource(ok ? R.drawable.bg_chip_good : R.drawable.bg_chip_bad);
        t.setPadding(dp(c, 8), dp(c, 5), dp(c, 8), dp(c, 5));
        return t;
    }

    public static LinearLayout card(Context c) {
        LinearLayout l = new LinearLayout(c);
        l.setOrientation(LinearLayout.VERTICAL);
        l.setBackgroundResource(R.drawable.bg_card);
        l.setPadding(dp(c, 12), dp(c, 10), dp(c, 12), dp(c, 10));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.bottomMargin = dp(c, 10);
        l.setLayoutParams(lp);
        return l;
    }

    public static void clear(LinearLayout box) { box.removeAllViews(); }

    public static void add(LinearLayout box, View v) { box.addView(v); }
}
