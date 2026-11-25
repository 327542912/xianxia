package com.novelengine.app;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.os.Bundle;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;
import com.novelengine.core.CostMeter;
import com.novelengine.core.CrossCheck;
import com.novelengine.core.Defaults;
import com.novelengine.core.LedgerParser;
import com.novelengine.core.Models;
import com.novelengine.core.OpenAiCompatProvider;
import com.novelengine.core.Pipeline;
import com.novelengine.core.ProviderFactory;
import com.novelengine.core.RunMode;
import com.novelengine.core.Slots;
import com.novelengine.core.ThreadItem;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

public class WriteActivity extends Activity {

    private SqliteRepository repo;
    private String projectId;
    private int chapterNo;
    private LinearLayout box;
    private TextView planView;
    private TextView statusView;
    private CostMeter meter = new CostMeter();
    private volatile boolean running = false;

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        setContentView(R.layout.activity_write);
        repo = new SqliteRepository(this);
        projectId = getIntent().getStringExtra("projectId");
        chapterNo = getIntent().getIntExtra("chapterNo", 1);
        box = (LinearLayout) findViewById(R.id.resultBox);
        planView = (TextView) findViewById(R.id.tvPlan);
        statusView = (TextView) findViewById(R.id.tvStatus);

        Models.AppConfig cfg = repo.loadConfig(projectId);
        Models.Outline outline = repo.loadOutline(projectId);
        if (cfg == null || outline == null) { finish(); return; }
        final Models.OutlineChapter plan = outline.chapter(chapterNo);
        if (plan == null) { finish(); return; }
        planView.setText("第" + plan.no + "章 " + plan.title + "\n核心事件：" + plan.coreEvent + "\n章末悬念：" + plan.cliffhanger);

        ((Button) findViewById(R.id.btnWrite)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { generateOne(false); }
        });
        ((Button) findViewById(R.id.btnRewrite)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { generateOne(true); }
        });
        ((Button) findViewById(R.id.btnRun)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { runAll(); }
        });
        ((Button) findViewById(R.id.btnVeto)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { veto(); }
        });
        ((Button) findViewById(R.id.btnNext)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                Models.Chapter c = repo.loadChapter(projectId, chapterNo);
                if (c == null || !"delivered".equals(c.status)) {
                    Toast.makeText(WriteActivity.this, "本章尚未通过闸门，不能进入下一章", Toast.LENGTH_LONG).show();
                    return;
                }
                Intent i = new Intent(WriteActivity.this, WriteActivity.class);
                i.putExtra("projectId", projectId);
                i.putExtra("chapterNo", chapterNo + 1);
                startActivity(i);
                finish();
            }
        });
        render(repo.loadChapter(projectId, chapterNo));
    }

    // ---------- 单章 ----------

    private Pipeline.Request buildRequest(int no, boolean dropPrevious) {
        Models.AppConfig cfg = repo.loadConfig(projectId);
        Models.Outline outline = repo.loadOutline(projectId);
        if (cfg == null || outline == null) return null;
        Models.OutlineChapter plan = outline.chapter(no);
        if (plan == null) return null;
        Models.StylePack pack = Defaults.pack(cfg.stylePackId);
        List<Models.Chapter> saved = repo.loadChapters(projectId);
        if (dropPrevious) {
            Models.Chapter old = repo.loadChapter(projectId, no);
            if (old != null) saved.remove(old);
        }
        Pipeline.Request r = new Pipeline.Request();
        r.cfg = cfg;
        r.pack = pack;
        r.clauses = Defaults.select(Defaults.clausesC(), Defaults.clausesM(), Defaults.clausesG(), cfg.genre, plan.stage);
        r.plan = plan;
        r.prev = no > 1 ? repo.loadChapter(projectId, no - 1) : null;
        r.lastLedger = r.prev == null ? null : r.prev.ledger;
        r.knownNames = knownNames(outline);
        r.historyBodies = bodies(saved);
        r.threads = threads(outline, no);
        r.slots = Slots.render(slotsData(outline, plan, r.prev, r.threads));
        r.injectionForEcho = (pack == null ? "" : pack.injection) + "\n" + r.slots;
        r.meter = meter;
        return r;
    }

    private void generateOne(boolean forceRewrite) {
        if (running) { Toast.makeText(this, "正在生成，请等待", Toast.LENGTH_SHORT).show(); return; }
        final Pipeline.Request r = buildRequest(chapterNo, forceRewrite);
        if (r == null) return;
        running = true;
        setBusy(true, "生成第" + chapterNo + "章…");
        new Thread(new Runnable() {
            @Override public void run() {
                try {
                    OpenAiCompatProvider p = (OpenAiCompatProvider) ProviderFactory.create(repo.loadProviderSettings());
                    final Models.Chapter ch = new Pipeline(p).generate(r);
                    repo.saveChapter(projectId, ch);
                    runOnUiThread(new Runnable() {
                        @Override public void run() {
                            running = false;
                            setBusy(false, "");
                            render(ch);
                        }
                    });
                } catch (final Exception e) {
                    runOnUiThread(new Runnable() {
                        @Override public void run() {
                            running = false;
                            setBusy(false, "生成失败：" + e.getMessage());
                        }
                    });
                }
            }
        }).start();
    }

    // ---------- 连跑（A 模式） ----------

    private void runAll() {
        if (running) { Toast.makeText(this, "正在生成，请等待", Toast.LENGTH_SHORT).show(); return; }
        final Models.Outline outline = repo.loadOutline(projectId);
        if (outline == null) return;
        final int last = outline.chapters.get(outline.chapters.size() - 1).no;
        final int count = Math.max(1, last - chapterNo + 1);
        running = true;
        setBusy(true, "连跑 " + count + " 章…");
        new Thread(new Runnable() {
            @Override public void run() {
                try {
                    final OpenAiCompatProvider provider = (OpenAiCompatProvider) ProviderFactory.create(repo.loadProviderSettings());
                    RunMode.Summary s = RunMode.run(chapterNo, count, new RunMode.ChapterWriter() {
                        @Override public Models.Chapter write(int no) throws Exception {
                            Pipeline.Request r = buildRequest(no, true);
                            if (r == null) throw new IllegalStateException("第" + no + "章缺大纲");
                            Models.Chapter ch = new Pipeline(provider).generate(r);
                            repo.saveChapter(projectId, ch);
                            return ch;
                        }
                    }, new RunMode.Listener() {
                        @Override public void onChapter(final int no, final Models.Chapter ch, final int done, final int total) {
                            runOnUiThread(new Runnable() {
                                @Override public void run() {
                                    setBusy(true, "已完成 " + done + "/" + total + "：第" + no + "章 " + ch.status
                                            + "（闸门 " + passCount(ch) + "/" + ch.gates.size() + "，重写 " + ch.rewrites + "）");
                                }
                            });
                        }
                        @Override public void onStop(final String reason, final int done) {
                            runOnUiThread(new Runnable() {
                                @Override public void run() {
                                    setBusy(false, "停止：" + reason + "（完成 " + done + " 章）");
                                }
                            });
                        }
                    });
                    final String text = "连跑结束 · 请求 " + s.requested + " 章 · 交付 " + s.delivered
                            + " · 挂起 " + s.held + " · " + s.stopReason + "\n" + meter.summary();
                    runOnUiThread(new Runnable() {
                        @Override public void run() {
                            running = false;
                            setBusy(false, text);
                            render(repo.loadChapter(projectId, chapterNo));
                        }
                    });
                } catch (final Exception e) {
                    runOnUiThread(new Runnable() {
                        @Override public void run() {
                            running = false;
                            setBusy(false, "连跑失败：" + e.getMessage());
                        }
                    });
                }
            }
        }).start();
    }

    private void veto() {
        Models.Chapter ch = repo.loadChapter(projectId, chapterNo);
        if (ch == null) { Toast.makeText(this, "本章尚未生成", Toast.LENGTH_SHORT).show(); return; }
        ch.status = "held";
        repo.saveChapter(projectId, ch);
        Models.HotfixEntry e = new Models.HotfixEntry();
        e.id = "HF-V" + System.currentTimeMillis() % 100000;
        e.date = System.currentTimeMillis();
        e.sample = "第" + ch.no + "章";
        e.symptom = "人工否决交付";
        e.cause = "ambiguous";
        e.action = "REVIEW-" + ch.no;
        e.layer = "C";
        e.regression = "待重写";
        repo.saveHotfix(projectId, e);
        render(ch);
        Toast.makeText(this, "已否决并记入台账", Toast.LENGTH_SHORT).show();
    }

    // ---------- 工具 ----------

    private void setBusy(boolean busy, String msg) {
        ((Button) findViewById(R.id.btnWrite)).setEnabled(!busy);
        ((Button) findViewById(R.id.btnRewrite)).setEnabled(!busy);
        ((Button) findViewById(R.id.btnRun)).setEnabled(!busy);
        if (msg != null && !msg.isEmpty()) statusView.setText(msg);
    }

    private int passCount(Models.Chapter ch) {
        int n = 0;
        for (Models.GateResult g : ch.gates) if (g.pass) n++;
        return n;
    }

    private List<String> knownNames(Models.Outline o) {
        Set<String> s = new HashSet<String>();
        for (Models.OutlineChapter c : o.chapters) for (String n : c.cast) s.add(n);
        for (Models.Relation r : o.relations) { s.add(r.a); s.add(r.b); }
        return new ArrayList<String>(s);
    }

    private List<String> bodies(List<Models.Chapter> cs) {
        List<String> out = new ArrayList<String>();
        for (Models.Chapter c : cs) if (c != null && c.text != null) out.add(c.text);
        return out;
    }

    private List<ThreadItem> threads(Models.Outline o, int upto) {
        List<ThreadItem> out = new ArrayList<ThreadItem>();
        for (Models.OutlineChapter c : o.chapters) {
            if (c.no > upto + 2) continue;
            for (int i = 0; i < c.threads.size(); i++) {
                out.add(new ThreadItem("T" + c.no + "-" + (i + 1), c.threads.get(i), c.no, c.no + 4));
            }
        }
        return out;
    }

    private Slots.Data slotsData(Models.Outline o, Models.OutlineChapter plan, Models.Chapter prev, List<ThreadItem> ts) {
        List<String[]> chars = new ArrayList<String[]>();
        if (prev != null && prev.ledger != null) {
            String p = prev.ledger.person;
            String[] parts = p == null ? new String[0] : p.split("[；;]");
            for (String part : parts) {
                String t = part.trim();
                int idx = t.indexOf('：');
                if (idx <= 0) continue;
                chars.add(new String[]{t.substring(0, idx).trim(), t.substring(idx + 1).trim(), "在场", String.valueOf(prev.no)});
            }
        }
        if (chars.isEmpty()) for (String n : plan.cast) chars.add(new String[]{n, "见本章", "待登场", String.valueOf(plan.no)});
        List<String[]> ths = new ArrayList<String[]>();
        int k = 0;
        for (ThreadItem t : ts) {
            if (t.closedChapter != 0) continue;
            ths.add(new String[]{t.id, t.content, "第" + t.seededChapter + "章", "第" + t.planChapter + "章回收"});
            if (++k >= 8) break;
        }
        String goal = o.mainline == null ? "" : (o.mainline.length() > 40 ? o.mainline.substring(0, 40) : o.mainline);
        List<String> done = new ArrayList<String>();
        for (Models.OutlineChapter c : o.chapters) if (c.no < plan.no) done.add("第" + c.no + "章 " + c.title);
        return Slots.fromProgress(goal, plan.stage, Math.max(1, 10 - plan.no + 1), plan.cliffhanger, chars, ths, done);
    }

    private void render(final Models.Chapter ch) {
        Ui.clear(box);
        if (ch == null) {
            box.addView(Ui.text(this, "尚未生成。可单章生成，或点「连跑到本卷末」由 A 模式连续推进。",
                    13, Color.parseColor("#5C6470")));
            return;
        }
        LinearLayout summary = Ui.card(this);
        summary.addView(Ui.section(this, "闸门结果（代码判定，模型不自报计数）"));
        summary.addView(Ui.text(this, "通过 " + passCount(ch) + "/" + ch.gates.size() + "　重写 " + ch.rewrites
                + " 次　状态 " + ch.status + "　耗时 " + (ch.ms / 1000) + "s　"
                + ch.tokensIn + "/" + ch.tokensOut + " tok", 13, Color.parseColor("#1B1D21")));
        for (Models.GateResult g : ch.gates) {
            LinearLayout row = new LinearLayout(this);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setPadding(0, Ui.dp(this, 3), 0, Ui.dp(this, 3));
            row.addView(Ui.chip(this, (g.pass ? "PASS " : "FAIL ") + g.id + " " + g.name, g.pass));
            row.addView(Ui.text(this, "  " + g.detail, 12, Color.parseColor("#5C6470")));
            summary.addView(row);
        }
        if (!ch.gatesPass()) {
            Button fix = new Button(this);
            fix.setText("把未通过项记入热修台账");
            fix.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) { logFailedGates(ch); }
            });
            summary.addView(fix);
        }
        box.addView(summary);

        List<CrossCheck.Item> cc = crossCheck(ch);
        LinearLayout ccc = Ui.card(this);
        ccc.addView(Ui.section(this, "四核对（角色 / 物件 / 时间 / 线头）"));
        for (CrossCheck.Item it : cc) {
            ccc.addView(Ui.text(this, (it.pass ? "✓ " : "✗ ") + it.id + " " + it.name + "　" + it.detail,
                    12, it.pass ? Color.parseColor("#1F7A4D") : Color.parseColor("#A33B2C")));
        }
        box.addView(ccc);

        if (ch.ledger != null) {
            LinearLayout lc = Ui.card(this);
            lc.addView(Ui.section(this, "跨章账本（已入库）"));
            lc.addView(Ui.mono(this, ch.ledger.render()));
            box.addView(lc);
        }
        if (!ch.verdicts.isEmpty()) {
            LinearLayout vc = Ui.card(this);
            vc.addView(Ui.section(this, "模型判定（只标问题，不改稿）"));
            for (Models.JudgeVerdict v : ch.verdicts) {
                boolean ok = "ok".equals(v.status);
                vc.addView(Ui.text(this, v.id + " " + v.name + " · " + v.status + "　" + v.note,
                        12, ok ? Color.parseColor("#1F7A4D") : Color.parseColor("#9A6A12")));
            }
            box.addView(vc);
        }
        LinearLayout tc = Ui.card(this);
        tc.addView(Ui.section(this, "正文"));
        tc.addView(Ui.text(this, LedgerParser.stripLedger(ch.text), 14, Color.parseColor("#1B1D21")));
        box.addView(tc);
    }

    private List<CrossCheck.Item> crossCheck(Models.Chapter ch) {
        CrossCheck.Input in = new CrossCheck.Input();
        in.chapter = ch;
        in.previous = chapterNo > 1 ? ledgerOf(repo.loadChapter(projectId, chapterNo - 1)) : null;
        Models.Outline o = repo.loadOutline(projectId);
        if (o != null) in.threads = threads(o, ch.no);
        return CrossCheck.run(in);
    }

    private Models.Ledger ledgerOf(Models.Chapter c) { return c == null ? null : c.ledger; }

    private void logFailedGates(Models.Chapter ch) {
        List<Models.GateResult> failed = ch.failedGates();
        for (Models.GateResult g : failed) {
            Models.HotfixEntry e = new Models.HotfixEntry();
            e.id = "HF-" + System.currentTimeMillis() % 100000 + "-" + g.id;
            e.date = System.currentTimeMillis();
            e.sample = "第" + ch.no + "章 " + g.id;
            e.symptom = g.detail;
            e.cause = "missing";
            e.action = "NEW-" + g.id;
            e.layer = "C";
            e.regression = "待回归";
            repo.saveHotfix(projectId, e);
        }
        Toast.makeText(this, "已记入台账 " + failed.size() + " 条", Toast.LENGTH_SHORT).show();
    }
}
