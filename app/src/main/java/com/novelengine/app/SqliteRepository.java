package com.novelengine.app;

import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteOpenHelper;
import com.novelengine.core.Json;
import com.novelengine.core.Models;
import com.novelengine.core.Repository;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

/** 真实持久化：SQLite。JSON 只作字段容器，索引与去重走表结构。 */
public class SqliteRepository extends SQLiteOpenHelper implements Repository {

    private static final String DB = "novelengine.db";
    private static final int VER = 1;
    private final android.content.SharedPreferences prefs;

    public SqliteRepository(Context ctx) {
        super(ctx, DB, null, VER);
        prefs = ctx.getSharedPreferences("engine_prefs", Context.MODE_PRIVATE);
    }

    @Override public void onCreate(SQLiteDatabase db) {
        db.execSQL("CREATE TABLE projects(id TEXT PRIMARY KEY, json TEXT NOT NULL)");
        db.execSQL("CREATE TABLE outlines(project_id TEXT PRIMARY KEY, json TEXT NOT NULL)");
        db.execSQL("CREATE TABLE chapters(project_id TEXT NOT NULL, no INTEGER NOT NULL, json TEXT NOT NULL, PRIMARY KEY(project_id, no))");
        db.execSQL("CREATE TABLE hotfix(project_id TEXT NOT NULL, id TEXT NOT NULL, json TEXT NOT NULL, PRIMARY KEY(project_id, id))");
        db.execSQL("CREATE TABLE meta(k TEXT PRIMARY KEY, v TEXT NOT NULL)");
    }

    @Override public void onUpgrade(SQLiteDatabase db, int oldV, int newV) { }

    @Override public Models.AppConfig loadConfig(String id) {
        SQLiteDatabase db = getReadableDatabase();
        Cursor c = db.query("projects", new String[]{"json"}, "id=?", new String[]{id}, null, null, null);
        try {
            if (c.moveToFirst()) return Models.AppConfig.fromJson(Json.obj(Json.parse(c.getString(0))));
        } finally { c.close(); }
        return null;
    }

    @Override public void saveConfig(String id, Models.AppConfig cfg) {
        ContentValues v = new ContentValues();
        v.put("id", id);
        v.put("json", Json.stringify(cfg.toJson()));
        getWritableDatabase().insertWithOnConflict("projects", null, v, SQLiteDatabase.CONFLICT_REPLACE);
    }

    @Override public Models.Outline loadOutline(String id) {
        SQLiteDatabase db = getReadableDatabase();
        Cursor c = db.query("outlines", new String[]{"json"}, "project_id=?", new String[]{id}, null, null, null);
        try {
            if (c.moveToFirst()) return Models.Outline.fromJson(Json.obj(Json.parse(c.getString(0))));
        } finally { c.close(); }
        return null;
    }

    @Override public void saveOutline(String id, Models.Outline o) {
        ContentValues v = new ContentValues();
        v.put("project_id", id);
        v.put("json", Json.stringify(o.toJson()));
        getWritableDatabase().insertWithOnConflict("outlines", null, v, SQLiteDatabase.CONFLICT_REPLACE);
    }

    @Override public List<Models.Chapter> loadChapters(String id) {
        List<Models.Chapter> out = new ArrayList<Models.Chapter>();
        Cursor c = getReadableDatabase().query("chapters", new String[]{"json"}, "project_id=?",
                new String[]{id}, null, null, "no ASC");
        try {
            while (c.moveToNext()) out.add(Models.Chapter.fromJson(Json.obj(Json.parse(c.getString(0)))));
        } finally { c.close(); }
        return out;
    }

    @Override public Models.Chapter loadChapter(String id, int no) {
        Cursor c = getReadableDatabase().query("chapters", new String[]{"json"}, "project_id=? AND no=?",
                new String[]{id, String.valueOf(no)}, null, null, null);
        try {
            if (c.moveToFirst()) return Models.Chapter.fromJson(Json.obj(Json.parse(c.getString(0))));
        } finally { c.close(); }
        return null;
    }

    @Override public void saveChapter(String id, Models.Chapter ch) {
        ContentValues v = new ContentValues();
        v.put("project_id", id);
        v.put("no", Integer.valueOf(ch.no));
        v.put("json", Json.stringify(ch.toJson()));
        getWritableDatabase().insertWithOnConflict("chapters", null, v, SQLiteDatabase.CONFLICT_REPLACE);
    }

    @Override public List<Models.HotfixEntry> loadHotfixes(String id) {
        List<Models.HotfixEntry> out = new ArrayList<Models.HotfixEntry>();
        Cursor c = getReadableDatabase().query("hotfix", new String[]{"json"}, "project_id=?",
                new String[]{id}, null, null, "id ASC");
        try {
            while (c.moveToNext()) out.add(Models.HotfixEntry.fromJson(Json.obj(Json.parse(c.getString(0)))));
        } finally { c.close(); }
        return out;
    }

    @Override public void saveHotfix(String id, Models.HotfixEntry e) {
        ContentValues v = new ContentValues();
        v.put("project_id", id);
        v.put("id", e.id);
        v.put("json", Json.stringify(e.toJson()));
        getWritableDatabase().insertWithOnConflict("hotfix", null, v, SQLiteDatabase.CONFLICT_REPLACE);
    }

    @Override public Models.ProviderSettings loadProviderSettings() {
        String raw = prefs.getString("provider", "");
        if (raw == null || raw.isEmpty()) return new Models.ProviderSettings();
        try {
            Models.ProviderSettings s = Models.ProviderSettings.fromJson(Json.obj(Json.parse(raw)));
            s.apiKey = prefs.getString("apiKey", "");
            return s;
        } catch (Exception e) {
            return new Models.ProviderSettings();
        }
    }

    @Override public void saveProviderSettings(Models.ProviderSettings s) {
        Map<String, Object> m = s.toJson();
        prefs.edit().putString("provider", Json.stringify(m)).putString("apiKey", s.apiKey).apply();
    }

    @Override public void saveMeta(String key, String value) {
        ContentValues v = new ContentValues();
        v.put("k", key);
        v.put("v", value);
        getWritableDatabase().insertWithOnConflict("meta", null, v, SQLiteDatabase.CONFLICT_REPLACE);
    }

    @Override public String loadMeta(String key, String def) {
        Cursor c = getReadableDatabase().query("meta", new String[]{"v"}, "k=?", new String[]{key}, null, null, null);
        try {
            if (c.moveToFirst()) return c.getString(0);
        } finally {
            c.close();
        }
        return def;
    }

    @Override public List<String> listProjects() {
        List<String> out = new ArrayList<String>();
        Cursor c = getReadableDatabase().query("projects", new String[]{"id"}, null, null, null, null, "id DESC");
        try {
            while (c.moveToNext()) out.add(c.getString(0));
        } finally { c.close(); }
        return out;
    }
}
