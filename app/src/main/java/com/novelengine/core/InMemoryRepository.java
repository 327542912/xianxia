package com.novelengine.core;

import com.novelengine.core.Models.AppConfig;
import com.novelengine.core.Models.Chapter;
import com.novelengine.core.Models.HotfixEntry;
import com.novelengine.core.Models.Outline;
import com.novelengine.core.Models.ProviderSettings;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** 测试用实现；与 SQLite 实现行为一致（同序、覆盖写）。 */
public class InMemoryRepository implements Repository {

    private final Map<String, AppConfig> configs = new LinkedHashMap<String, AppConfig>();
    private final Map<String, Outline> outlines = new LinkedHashMap<String, Outline>();
    private final Map<String, Map<Integer, Chapter>> chapters = new LinkedHashMap<String, Map<Integer, Chapter>>();
    private final Map<String, List<HotfixEntry>> hotfixes = new LinkedHashMap<String, List<HotfixEntry>>();
    private ProviderSettings settings = new ProviderSettings();
    private final Map<String, String> meta = new LinkedHashMap<String, String>();

    @Override public AppConfig loadConfig(String id) { return configs.get(id); }

    @Override public void saveConfig(String id, AppConfig c) {
        configs.put(id, c);
        if (!outlines.containsKey(id)) outlines.put(id, null);
        chapters.putIfAbsent(id, new LinkedHashMap<Integer, Chapter>());
        hotfixes.putIfAbsent(id, new ArrayList<HotfixEntry>());
    }

    @Override public Outline loadOutline(String id) { return outlines.get(id); }

    @Override public void saveOutline(String id, Outline o) { outlines.put(id, o); }

    @Override public List<Chapter> loadChapters(String id) {
        List<Chapter> out = new ArrayList<Chapter>();
        Map<Integer, Chapter> m = chapters.get(id);
        if (m == null) return out;
        List<Integer> keys = new ArrayList<Integer>(m.keySet());
        java.util.Collections.sort(keys);
        for (Integer k : keys) out.add(m.get(k));
        return out;
    }

    @Override public Chapter loadChapter(String id, int no) {
        Map<Integer, Chapter> m = chapters.get(id);
        return m == null ? null : m.get(Integer.valueOf(no));
    }

    @Override public void saveChapter(String id, Chapter c) {
        Map<Integer, Chapter> m = chapters.get(id);
        if (m == null) { m = new LinkedHashMap<Integer, Chapter>(); chapters.put(id, m); }
        m.put(Integer.valueOf(c.no), c);
    }

    @Override public List<HotfixEntry> loadHotfixes(String id) {
        List<HotfixEntry> l = hotfixes.get(id);
        return l == null ? new ArrayList<HotfixEntry>() : l;
    }

    @Override public void saveHotfix(String id, HotfixEntry e) {
        List<HotfixEntry> l = hotfixes.get(id);
        if (l == null) { l = new ArrayList<HotfixEntry>(); hotfixes.put(id, l); }
        l.add(e);
    }

    @Override public ProviderSettings loadProviderSettings() { return settings; }

    @Override public void saveProviderSettings(ProviderSettings s) { settings = s; }

    @Override public List<String> listProjects() { return new ArrayList<String>(configs.keySet()); }

    @Override public void saveMeta(String key, String value) { meta.put(key, value); }

    @Override public String loadMeta(String key, String def) {
        String v = meta.get(key);
        return v == null ? def : v;
    }
}
