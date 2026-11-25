package com.novelengine.core;

import org.junit.Test;
import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public class LedgerParserTest {

    private static final String GOOD =
        "正文第一段。\n\n正文第二段。\n\n【跨章账本】第3章\n事件：他在契约上按了手印\n物件：契约书在怀里，墨迹已干\n人物：秦大：外门舍房，整夜未眠\n时间：下矿前夜，秋，第三日\n信息：他知道名额只剩一个\n线索：T3-1 欠条下落";

    @Test public void parsesSixFields() {
        LedgerParser.Result r = LedgerParser.parse(GOOD, 3);
        assertTrue(r.ok());
        assertEquals("他在契约上按了手印", r.ledger.event);
        assertEquals(3, r.ledger.chapter);
    }

    @Test public void detectsMissingField() {
        String bad = GOOD.replace("时间：下矿前夜，秋，第三日\n", "");
        LedgerParser.Result r = LedgerParser.parse(bad, 3);
        assertFalse(r.ok());
        assertTrue(r.problems.toString().contains("时间"));
    }

    @Test public void detectsChapterMismatch() {
        LedgerParser.Result r = LedgerParser.parse(GOOD, 4);
        assertFalse(r.ok());
        assertTrue(r.problems.toString().contains("第4章"));
    }

    @Test public void stripLedgerKeepsBodyOnly() {
        String body = LedgerParser.stripLedger(GOOD);
        assertFalse(body.contains("跨章账本"));
        assertTrue(body.contains("正文第一段"));
    }

    @Test public void extractsPersonNames() {
        LedgerParser.Result r = LedgerParser.parse(GOOD, 3);
        assertTrue(LedgerParser.personNames(r.ledger).contains("秦大"));
    }
}
