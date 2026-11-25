package com.novelengine.app;

import android.app.Activity;
import android.os.Bundle;
import android.view.View;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.Spinner;
import android.widget.TextView;
import com.novelengine.core.Models;
import com.novelengine.core.OpenAiCompatProvider;
import com.novelengine.core.ProviderFactory;
import java.util.ArrayList;
import java.util.List;

public class SettingsActivity extends Activity {

    private SqliteRepository repo;

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        setContentView(R.layout.activity_settings);
        repo = new SqliteRepository(this);
        final Models.ProviderSettings s = repo.loadProviderSettings();

        set(R.id.fLocalUrl, s.localBaseUrl);
        set(R.id.fLocalModel, s.localModel);
        set(R.id.fApiUrl, s.apiBaseUrl);
        set(R.id.fApiModel, s.apiModel);
        set(R.id.fApiKey, s.apiKey);

        final Spinner mode = (Spinner) findViewById(R.id.spMode);
        List<String> modes = new ArrayList<String>();
        modes.add("本地端点（llama.cpp / Ollama）");
        modes.add("API（OpenAI 兼容，需密钥）");
        mode.setAdapter(new ArrayAdapter<String>(this, android.R.layout.simple_spinner_dropdown_item, modes));
        mode.setSelection("api".equalsIgnoreCase(s.mode) ? 1 : 0);

        ((Button) findViewById(R.id.btnTest)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                Models.ProviderSettings cur = collect(s);
                ((TextView) findViewById(R.id.tvTest)).setText("测试中…");
                try {
                    final OpenAiCompatProvider p = (OpenAiCompatProvider) ProviderFactory.create(cur);
                    new Thread(new Runnable() {
                        @Override public void run() {
                            final String r = p.test();
                            runOnUiThread(new Runnable() {
                                @Override public void run() { ((TextView) findViewById(R.id.tvTest)).setText(r); }
                            });
                        }
                    }).start();
                } catch (Exception e) {
                    ((TextView) findViewById(R.id.tvTest)).setText("配置有误：" + e.getMessage());
                }
            }
        });
        ((Button) findViewById(R.id.btnSaveProv)).setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                repo.saveProviderSettings(collect(s));
                finish();
            }
        });
    }

    private Models.ProviderSettings collect(Models.ProviderSettings s) {
        s.localBaseUrl = get(R.id.fLocalUrl);
        s.localModel = get(R.id.fLocalModel);
        s.apiBaseUrl = get(R.id.fApiUrl);
        s.apiModel = get(R.id.fApiModel);
        s.apiKey = get(R.id.fApiKey);
        s.mode = ((Spinner) findViewById(R.id.spMode)).getSelectedItemPosition() == 1 ? "api" : "local";
        return s;
    }

    private void set(int id, String v) { ((EditText) findViewById(id)).setText(v == null ? "" : v); }

    private String get(int id) { return ((EditText) findViewById(id)).getText().toString().trim(); }
}
