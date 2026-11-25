package com.novelengine.core;

/** 线头登记。悬空判定用真实章号差，不靠模型记忆。 */
public class ThreadItem {
    public String id = "";
    public String content = "";
    public int seededChapter = 0;
    public int planChapter = 0;
    public int closedChapter = 0;

    public ThreadItem() {}

    public ThreadItem(String id, String content, int seededChapter, int planChapter) {
        this.id = id; this.content = content; this.seededChapter = seededChapter; this.planChapter = planChapter;
    }

    public boolean open() { return closedChapter == 0; }

    public int age(int currentChapter) {
        return currentChapter - seededChapter;
    }
}
