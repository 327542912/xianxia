package com.novelengine.app;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.graphics.Color;
import android.os.Bundle;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;
import com.novelengine.core.Defaults;
import com.novelengine.core.Models;
import com.novelengine.core.OpenAiCompatProvider;
import com.novelengine.core.Planner;
import com.novelengine.core.ProviderFactory;

public class PlanActivity extends Activity {

    private SqliteRepository repo;
    private String projectId;
    private LinearLayout box;
    private TextView sub;
    private Thread worker;

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        setContentView(R.layout.activity_plan);
        repo = new SqliteRepository(this);
        projectId = getIntent().getStringExtra("projectId");
        box = (LinearLayout) findViewById(R.id.outlineBox);
        sub = (TextView) findViewById(R.id.subLine);

        Models.AppConfig c = repo.loadConfig(projectId);
        if (c == null) { finish(); return; }
        sub.setText(c.title + " · " + c.genre + " · " + c.hero);
        render(repo.loadOutline(projectId));

        ((Button) findViewById(R.id.btnPlan)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { runPlan(); }
        });
        ((Button) findViewById(R.id.btnQa)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                Intent i = new Intent(PlanActivity.this, QaActivity.class);
                i.putExtra("projectId", projectId);
                startActivity(i);
            }
        });
    }

    private void runPlan() {
        final Models.AppConfig c = repo.loadConfig(projectId);
        if (c == null) return;
        if (!c.locked) { Toast.makeText(this, "请先在配置页锁定配置", Toast.LENGTH_SHORT).show(); return; }
        final Models.StylePack pack = Defaults.pack(c.stylePackId);
        ((Button) findViewById(R.id.btnPlan)).setEnabled(false);
        sub.setText("策划中…若本地模型较慢，请等待（超时 " + repo.loadProviderSettings().timeoutSec + " 秒）");
        worker = new Thread(new Runnable() {
            @Override public void run() {
                try {
                    OpenAiCompatProvider p = (OpenAiCompatProvider) ProviderFactory.create(repo.loadProviderSettings());
                    Planner planner = new Planner(p);
                    final Models.Outline o = planner.plan(c, pack);
                    repo.saveOutline(projectId, o);
                    runOnUiThread(new Runnable() {
                        @Override public void run() {
                            render(o);
                            sub.setText("策划已生成并保存 · " + o.chapters.size() + " 章 · 模型 " + p.modelName());
                        }
                    });
                } catch (final Exception e) {
                    runOnUiThread(new Runnable() {
                        @Override public void run() {
                            sub.setText("策划失败");
                            new AlertDialog.Builder(PlanActivity.this)
                                    .setTitle("策划未完成")
                                    .setMessage(String.valueOf(e.getMessage()))
                                    .setPositiveButton("知道了", null).show();
                        }
                    });
                } finally {
                    runOnUiThread(new Runnable() {
                        @Override public void run() { ((Button) findViewById(R.id.btnPlan)).setEnabled(true); }
                    });
                }
            }
        });
        worker.start();
    }

    private void render(final Models.Outline o) {
        Ui.clear(box);
        if (o == null) {
            box.addView(Ui.text(this, "尚未生成。点下方按钮，用三要素扩成主线、人物关系、爽点、反转与 10 章大纲。",
                    13, Color.parseColor("#5C6470")));
            return;
        }
        LinearLayout head = Ui.card(this);
        head.addView(Ui.section(this, "一句话"));
        head.addView(Ui.text(this, o.logline, 15, Color.parseColor("#1B1D21")));
        head.addView(Ui.section(this, "主线"));
        head.addView(Ui.text(this, o.mainline, 14, Color.parseColor("#1B1D21")));
        if (!o.factions.isEmpty()) {
            head.addView(Ui.section(this, "势力"));
            head.addView(Ui.text(this, join(o.factions.toArray(new String[0]), " · "), 13, Color.parseColor("#5C6470")));
        }
        box.addView(head);

        if (!o.relations.isEmpty()) {
            LinearLayout card = Ui.card(this);
            card.addView(Ui.section(this, "人物关系（" + o.relations.size() + "）"));
            for (Models.Relation r : o.relations) {
                card.addView(Ui.text(this, r.a + " ↔ " + r.b + "：" + r.relation + "　压力：" + r.tension,
                        13, Color.parseColor("#1B1D21")));
            }
            box.addView(card);
        }
        if (!o.hooks.isEmpty()) {
            LinearLayout card = Ui.card(this);
            card.addView(Ui.section(this, "爽点（" + o.hooks.size() + "）"));
            for (Models.Beat x : o.hooks) {
                card.addView(Ui.text(this, "· " + x.name + "　" + x.where + "　" + x.payoff, 13, Color.parseColor("#1B1D21")));
            }
            box.addView(card);
        }
        if (!o.twists.isEmpty()) {
            LinearLayout card = Ui.card(this);
            card.addView(Ui.section(this, "反转与伏笔（" + o.twists.size() + "）"));
            for (Models.Beat x : o.twists) {
                card.addView(Ui.text(this, "· " + x.name + "　埋于 " + x.where + "　兑现 " + x.payoff, 13, Color.parseColor("#1B1D21")));
            }
            box.addView(card);
        }

        LinearLayout chCard = Ui.card(this);
        chCard.addView(Ui.section(this, "前 10 章大纲（点章节进入写作）"));
        for (final Models.OutlineChapter ch : o.chapters) {
            Models.Chapter saved = repo.loadChapter(projectId, ch.no);
            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.VERTICAL);
            row.setPadding(0, Ui.dp(this, 8), 0, Ui.dp(this, 8));
            row.addView(Ui.text(this, "第" + ch.no + "章　" + ch.title + (saved == null ? "" : "　（已成稿）"),
                    14, Color.parseColor("#1B1D21")));
            row.addView(Ui.text(this, "核心事件：" + ch.coreEvent, 12, Color.parseColor("#5C6470")));
            row.addView(Ui.text(this, "章末悬念：" + ch.cliffhanger, 12, Color.parseColor("#5C6470")));
            row.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) {
                    Intent i = new Intent(PlanActivity.this, WriteActivity.class);
                    i.putExtra("projectId", projectId);
                    i.putExtra("chapterNo", ch.no);
                    startActivity(i);
                }
            });
            chCard.addView(row);
        }
        box.addView(chCard);
    }

    private static String join(String[] xs, String sep) {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < xs.length; i++) { if (i > 0) sb.append(sep); sb.append(xs[i]); }
        return sb.toString();
    }
}
