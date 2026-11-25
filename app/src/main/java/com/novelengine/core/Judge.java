package com.novelengine.core;

import com.novelengine.core.Models.JudgeVerdict;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

/** M01–M08：模型判定，只标问题不改稿；判定失败如实记录，不伪造通过。 */
public final class Judge {

    private Judge() {}

    public static List<JudgeVerdict> run(Provider provider, String body, String prevTails) {
        List<JudgeVerdict> out = new ArrayList<JudgeVerdict>();
        try {
            Provider.Result r = provider.complete(Prompts.JUDGE_SYSTEM, Prompts.judgeUser(body, prevTails), 900, 0.1);
            String json = r.text.trim();
            int s = json.indexOf('[');
            int e = json.lastIndexOf(']');
            if (s < 0 || e <= s) throw new IllegalStateException("判定返回非数组");
            Object parsed = Json.parse(json.substring(s, e + 1));
            for (Object o : Json.arr(parsed)) {
                Map<String, Object> m = Json.obj(o);
                JudgeVerdict v = new JudgeVerdict();
                v.id = Json.str(m, "id", "");
                v.name = Json.str(m, "name", "");
                v.status = Json.str(m, "status", "warn");
                v.note = Json.str(m, "note", "");
                v.evidence = Json.str(m, "evidence", "");
                if (!v.id.isEmpty()) out.add(v);
            }
        } catch (Exception ex) {
            JudgeVerdict v = new JudgeVerdict();
            v.id = "MX";
            v.name = "判定未执行";
            v.status = "warn";
            v.note = "模型判定失败：" + ex.getMessage() + "。本项不计入通过，需人工抽样确认。";
            out.add(v);
        }
        return out;
    }
}
