package com.novelengine.app;

import android.content.Context;
import android.content.Intent;
import com.novelengine.core.LedgerParser;
import com.novelengine.core.Models;
import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStreamWriter;
import java.util.List;

/** 导出：写入应用外部目录并唤起分享；返回文件路径供 UI 提示。 */
public final class Export {

    private Export() {}

    public static String shareAll(Context ctx, SqliteRepository repo, String projectId) {
        try {
            Models.AppConfig cfg = repo.loadConfig(projectId);
            List<Models.Chapter> cs = repo.loadChapters(projectId);
            if (cs.isEmpty()) return null;
            StringBuilder sb = new StringBuilder();
            sb.append(cfg == null ? "未命名" : cfg.title).append("\n\n");
            for (Models.Chapter c : cs) {
                sb.append("第").append(c.no).append("章 ").append(c.title == null ? "" : c.title).append("\n\n");
                sb.append(LedgerParser.stripLedger(c.text)).append("\n\n");
                if (c.ledger != null) sb.append(c.ledger.render()).append("\n\n");
            }
            File dir = ctx.getExternalFilesDir("export");
            if (dir == null) dir = ctx.getFilesDir();
            File f = new File(dir, safe(cfg == null ? "novel" : cfg.title) + ".txt");
            FileOutputStream fos = new FileOutputStream(f);
            OutputStreamWriter w = new OutputStreamWriter(fos, "UTF-8");
            try {
                w.write(sb.toString());
                w.flush();
            } finally {
                w.close();
            }
            Intent send = new Intent(Intent.ACTION_SEND);
            send.setType("text/plain");
            send.putExtra(Intent.EXTRA_TEXT, sb.length() > 20000 ? sb.substring(0, 20000) : sb.toString());
            ctx.startActivity(Intent.createChooser(send, "分享全书"));
            return f.getAbsolutePath();
        } catch (Exception e) {
            return null;
        }
    }

    private static String safe(String s) {
        if (s == null || s.trim().isEmpty()) return "novel";
        return s.replaceAll("[\\\\/:*?\"<>|]", "_");
    }
}
