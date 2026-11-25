package com.novelengine.core;

/** 模型接入抽象。本地与 API 走同一纸契约，上层不感知来源。 */
public interface Provider {

    class Result {
        public String text = "";
        public int tokensIn = 0;
        public int tokensOut = 0;
        public long ms = 0;
        public String model = "";
    }

    Result complete(String system, String user, int maxTokens, double temperature) throws Exception;

    String name();

    boolean isLocal();

    class ProviderException extends Exception {
        public final int code;
        public ProviderException(String message, int code) { super(message); this.code = code; }
        public ProviderException(String message, Throwable cause) { super(message, cause); this.code = -1; }
    }
}
