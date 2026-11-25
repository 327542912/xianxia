package com.novelengine.app;

import android.app.Activity;
import android.graphics.Color;
import android.os.Bundle;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;
import com.novelengine.core.CostMeter;
import com.novelengine.core.Models;
import java.util.List;

public class QaActivity extends Activity {

    private SqliteRepository repo;
    private String projectId;
    private LinearLayout box;

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        setContentView(R.layout.activity_qa);
        repo = new SqliteRepository(this);
        projectId = getIntent().getStringExtra("projectId");
        box = (LinearLayout) findViewById(R.id.qaBox);
        render();
        ((Button) findViewById(R.id.btnExport)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                String path = Export.shareAll(QaActivity.this, repo, projectId);
                Toast.makeText(QaActivity.this, path == null ? "导出失败" : ("已导出：" + path), Toast.LENGTH_LONG).show();
            }
        });
    }

    private void render() {
        Ui.clear(box);
        List<Models.Chapter> cs = repo.loadChapters(projectId);
        Models.AppConfig cfg = repo.loadConfig(projectId);
        CostMeter meter = new CostMeter();
        int delivered = 0, held = 0, gateTotal = 0, gatePass = 0;
        for (Models.Chapter c : cs) {
            if ("delivered".equals(c.status)) delivered++; else held++;
            for (Models.GateResult g : c.gates) { gateTotal++; if (g.pass) gatePass++; }
            meter.record(c.tokensIn, c.tokensOut, false);
        }
        LinearLayout s = Ui.card(this);
        s.addView(Ui.section(this, "总览"));
        s.addView(Ui.text(this, (cfg == null ? "" : cfg.title) + "　已交付 " + delivered + " 章　挂起 " + held + " 章",
                15, Color.parseColor("#1B1D21")));
        s.addView(Ui.text(this, "闸门通过 " + gatePass + "/" + gateTotal + " 项"
                + (gateTotal == 0 ? "" : String.format(java.util.Locale.US, "（%.0f%%）", gatePass * 100.0 / gateTotal)),
                13, Color.parseColor("#5C6470")));
        s.addView(Ui.text(this, meter.summary(), 12, Color.parseColor("#5C6470")));
        box.addView(s);

        LinearLayout qc = Ui.card(this);
        qc.addView(Ui.section(this, "逐章"));
        if (cs.isEmpty()) qc.addView(Ui.text(this, "还没有成稿章节。", 13, Color.parseColor("#5C6470")));
        for (Models.Chapter c : cs) {
            int p = 0;
            for (Models.GateResult g : c.gates) if (g.pass) p++;
            qc.addView(Ui.text(this, "第" + c.no + "章 " + c.title + "　" + c.status + "　闸门 "
                    + p + "/" + c.gates.size() + "　重写 " + c.rewrites, 13, Color.parseColor("#1B1D21")));
        }
        box.addView(qc);

        LinearLayout hc = Ui.card(this);
        List<Models.HotfixEntry> hs = repo.loadHotfixes(projectId);
        hc.addView(Ui.section(this, "热修台账（" + hs.size() + "）"));
        if (hs.isEmpty()) hc.addView(Ui.text(this, "暂无。闸门不通过时可从写作页记入。", 13, Color.parseColor("#5C6470")));
        for (Models.HotfixEntry e : hs) {
            hc.addView(Ui.text(this, e.id + " · " + e.sample + " · 归因 " + e.cause + " · " + e.action
                    + " · " + e.status + " · " + e.regression, 12, Color.parseColor("#5C6470")));
        }
        box.addView(hc);
    }
}
