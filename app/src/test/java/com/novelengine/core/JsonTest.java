package com.novelengine.core;

import org.junit.Test;
import java.util.List;
import java.util.Map;
import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

public class JsonTest {

    @Test public void roundTripObject() {
        String src = "{\"a\":1,\"b\":\"文\\n字\",\"c\":[true,null,2.5],\"d\":{\"e\":\"x\"}}";
        Object v = Json.parse(src);
        Map<String, Object> m = Json.obj(v);
        assertEquals("文\n字", Json.str(m, "b", ""));
        assertEquals(1, Json.num(m, "a", 0));
        List<Object> arr = Json.arr(m.get("c"));
        assertEquals(3, arr.size());
        String out = Json.stringify(v);
        Map<String, Object> again = Json.obj(Json.parse(out));
        assertEquals(Json.str(m, "b", ""), Json.str(again, "b", ""));
    }

    @Test public void handlesUnicodeEscape() {
        Map<String, Object> m = Json.obj(Json.parse("{\"k\":\"\\u4e2d\"}"));
        assertEquals("中", Json.str(m, "k", ""));
    }

    @Test public void prettyKeepsNesting() {
        String s = Json.pretty(Json.parse("{\"a\":[1,2],\"b\":{\"c\":1}}"));
        assertTrue(s.contains("\n"));
        assertTrue(s.contains("\"a\""));
    }

    @Test(expected = Json.JsonException.class) public void rejectsTrailing() {
        Json.parse("{}x");
    }
}
