package com.novelengine.core;

import com.novelengine.core.Models.AppConfig;
import com.novelengine.core.Models.Chapter;
import com.novelengine.core.Models.HotfixEntry;
import com.novelengine.core.Models.Outline;
import com.novelengine.core.Models.ProviderSettings;
import java.util.List;

/** 存储抽象：Android 侧用 SQLite 实现，测试侧用内存实现，上层逻辑不感知。 */
public interface Repository {

    AppConfig loadConfig(String projectId);

    void saveConfig(String projectId, AppConfig config);

    Outline loadOutline(String projectId);

    void saveOutline(String projectId, Outline outline);

    List<Chapter> loadChapters(String projectId);

    Chapter loadChapter(String projectId, int no);

    void saveChapter(String projectId, Chapter chapter);

    List<HotfixEntry> loadHotfixes(String projectId);

    void saveHotfix(String projectId, HotfixEntry entry);

    ProviderSettings loadProviderSettings();

    void saveProviderSettings(ProviderSettings settings);

    List<String> listProjects();

    void saveMeta(String key, String value);

    String loadMeta(String key, String def);
}
