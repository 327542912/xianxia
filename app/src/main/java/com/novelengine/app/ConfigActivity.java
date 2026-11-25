package com.novelengine.app;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.os.Bundle;
import android.view.View;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.Spinner;
import android.widget.TextView;
import android.widget.Toast;
import com.novelengine.core.Defaults;
import com.novelengine.core.Models;
import java.util.ArrayList;
import java.util.List;

public class ConfigActivity extends Activity {

    private SqliteRepository repo;
    private String projectId;
    private List<Models.StylePack> packs;

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        setContentView(R.layout.activity_config);
        repo = new SqliteRepository(this);
        projectId = getIntent().getStringExtra("projectId");
        if (projectId == null) projectId = "p" + System.currentTimeMillis();

        packs = Defaults.stylePacks();
        List<String> names = new ArrayList<String>();
        for (Models.StylePack p : packs) names.add(p.name + "（" + p.genre + "）");
        Spinner sp = (Spinner) findViewById(R.id.spStyle);
        sp.setAdapter(new ArrayAdapter<String>(this, android.R.layout.simple_spinner_dropdown_item, names));

        Spinner tier = (Spinner) findViewById(R.id.spTier);
        List<String> tiers = new ArrayList<String>();
        tiers.add("本地模型");
        tiers.add("API 模型");
        tier.setAdapter(new ArrayAdapter<String>(this, android.R.layout.simple_spinner_dropdown_item, tiers));

        Models.AppConfig c = repo.loadConfig(projectId);
        if (c != null) fill(c, sp, tier);

        ((Button) findViewById(R.id.btnSave)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { save(false); }
        });
        ((Button) findViewById(R.id.btnGoPlan)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { save(true); }
        });
    }

    private void fill(Models.AppConfig c, Spinner sp, Spinner tier) {
        set(R.id.fTitle, c.title); set(R.id.fGenre, c.genre); set(R.id.fPlatform, c.platform);
        set(R.id.fHero, c.hero); set(R.id.fConflict, c.conflict); set(R.id.fWorld, c.world);
        set(R.id.fResource, c.resource); set(R.id.fChars, String.valueOf(c.targetChars));
        for (int i = 0; i < packs.size(); i++) if (packs.get(i).id.equals(c.stylePackId)) sp.setSelection(i);
        tier.setSelection("api".equalsIgnoreCase(c.modelTier) ? 1 : 0);
    }

    private void set(int id, String v) { ((EditText) findViewById(id)).setText(v == null ? "" : v); }

    private String get(int id) { return ((EditText) findViewById(id)).getText().toString().trim(); }

    private void save(boolean goPlan) {
        Models.AppConfig c = new Models.AppConfig();
        c.title = get(R.id.fTitle);
        c.genre = get(R.id.fGenre);
        c.platform = get(R.id.fPlatform);
        c.hero = get(R.id.fHero);
        c.conflict = get(R.id.fConflict);
        c.world = get(R.id.fWorld);
        c.resource = get(R.id.fResource);
        try { c.targetChars = Integer.parseInt(get(R.id.fChars)); } catch (Exception e) { c.targetChars = 2400; }
        Spinner sp = (Spinner) findViewById(R.id.spStyle);
        c.stylePackId = packs.get(sp.getSelectedItemPosition()).id;
        Spinner tier = (Spinner) findViewById(R.id.spTier);
        c.modelTier = tier.getSelectedItemPosition() == 1 ? "api" : "local";
        c.locked = true;
        c.lockedAt = System.currentTimeMillis();
        try {
            c.validate();
        } catch (IllegalStateException e) {
            Toast.makeText(this, e.getMessage(), Toast.LENGTH_LONG).show();
            return;
        }
        repo.saveConfig(projectId, c);
        Toast.makeText(this, "配置已锁定", Toast.LENGTH_SHORT).show();
        if (goPlan) {
            Intent i = new Intent(this, PlanActivity.class);
            i.putExtra("projectId", projectId);
            startActivity(i);
        }
        finish();
    }
}
