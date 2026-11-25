package com.novelengine.core;

import com.novelengine.core.Models.ProviderSettings;

/** 依据设置装配真实适配器；本地与 API 走同一实现，差别只在端点与鉴权。 */
public final class ProviderFactory {

    private ProviderFactory() {}

    public static Provider create(ProviderSettings s) {
        if (s == null) throw new IllegalStateException("模型设置缺失");
        if ("api".equalsIgnoreCase(s.mode)) {
            if (s.apiKey == null || s.apiKey.trim().isEmpty()) {
                throw new IllegalStateException("API 模式需要填写密钥（仅保存在本机）");
            }
            return new OpenAiCompatProvider(s.apiBaseUrl, s.apiModel, s.apiKey, s.timeoutSec, false);
        }
        return new OpenAiCompatProvider(s.localBaseUrl, s.localModel, "", s.timeoutSec, true);
    }

    public static String describe(ProviderSettings s) {
        if (s == null) return "未配置";
        return "api".equalsIgnoreCase(s.mode) ? ("API · " + s.apiModel) : ("本地 · " + s.localModel);
    }
}
