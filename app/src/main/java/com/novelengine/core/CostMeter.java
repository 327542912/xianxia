package com.novelengine.core;

/** 成本与预算：按真实 usage 记账；provider 未返回 usage 时用估算值，并标注口径。 */
public class CostMeter {

    public long tokensIn = 0;
    public long tokensOut = 0;
    public int chapters = 0;
    public double priceInPerMillion = 2.0;
    public double priceOutPerMillion = 8.0;
    public int estimatedCount = 0;

    public void record(int tin, int tout, boolean estimated) {
        tokensIn += tin;
        tokensOut += tout;
        chapters++;
        if (estimated) estimatedCount++;
    }

    public double cost() {
        return tokensIn / 1_000_000.0 * priceInPerMillion + tokensOut / 1_000_000.0 * priceOutPerMillion;
    }

    public int avgIn() { return chapters == 0 ? 0 : (int) (tokensIn / chapters); }

    public int avgOut() { return chapters == 0 ? 0 : (int) (tokensOut / chapters); }

    public String summary() {
        return String.format(java.util.Locale.US,
                "章数 %d · 输入 %d tok（均 %d）· 输出 %d tok（均 %d）· 估价 %.4f 元%s",
                chapters, tokensIn, avgIn(), tokensOut, avgOut(), cost(),
                estimatedCount == 0 ? "" : "（含 " + estimatedCount + " 章为估算口径）");
    }
}
