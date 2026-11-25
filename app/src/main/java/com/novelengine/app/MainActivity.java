package com.novelengine.app;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.os.Bundle;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;
import com.novelengine.core.Models;
import com.novelengine.core.ProviderFactory;
import java.util.List;

public class MainActivity extends Activity {

    private SqliteRepository repo;
    private LinearLayout box;
    private TextView modelLine;

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        setContentView(R.layout.activity_main);
        repo = new SqliteRepository(this);
        box = (LinearLayout) findViewById(R.id.projects);
        modelLine = (TextView) findViewById(R.id.modelLine);

        ((Button) findViewById(R.id.btnNew)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                String id = "p" + System.currentTimeMillis();
                Intent i = new Intent(MainActivity.this, ConfigActivity.class);
                i.putExtra("projectId", id);
                startActivity(i);
            }
        });
        ((Button) findViewById(R.id.btnSettings)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { startActivity(new Intent(MainActivity.this, SettingsActivity.class)); }
        });
    }

    @Override protected void onResume() {
        super.onResume();
        refresh();
    }

    private void refresh() {
        if (box == null) return;
        Ui.clear(box);
        List<String> ids = repo.listProjects();
        if (ids.isEmpty()) {
            box.addView(Ui.text(this, "还没有作品。点上方的「新建作品」，填三要素即可开始。", 13, Color.parseColor("#5C6470")));
        }
        for (int i = 0; i < ids.size(); i++) {
            final String id = ids.get(i);
            Models.AppConfig c = repo.loadConfig(id);
            if (c == null) continue;
            LinearLayout card = Ui.card(this);
            card.addView(Ui.text(this, c.title, 16, Color.parseColor("#1B1D21")));
            card.addView(Ui.text(this, c.genre + " · " + (c.platform == null ? "" : c.platform) + " · " + c.targetChars + " 字/章",
                    12, Color.parseColor("#5C6470")));
            Models.Outline o = repo.loadOutline(id);
            int done = repo.loadChapters(id).size();
            card.addView(Ui.text(this,
                    (c.locked ? "已锁定" : "未锁定") + " ｜ 大纲：" + (o == null ? "未生成" : (o.chapters.size() + " 章"))
                            + " ｜ 已成稿：" + done + " 章", 12, Color.parseColor("#5C6470")));
            card.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) {
                    Intent it = new Intent(MainActivity.this, PlanActivity.class);
                    it.putExtra("projectId", id);
                    startActivity(it);
                }
            });
            box.addView(card);
        }
        modelLine.setText("当前模型：" + ProviderFactory.describe(repo.loadProviderSettings()));
    }
}
