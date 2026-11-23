import java.util.*;

/**
 * P0 无头世界模拟器原型 v2（toy scale：64 节点 / 开局 9000 散人 / 500 年）
 * 验证需求文档 11 章 1~9 条。
 */
public class SimP0 {

    static final int YEARS = 500;
    static final int NODE_W = 8;
    static final int NODES = NODE_W * NODE_W;
    static final int POP_CAP = 70000;
    static final int INIT_POP = 9000;
    static final int TRUST_ORG = 2;      // 重复交易达此值算稳定关系
    static final int COMP_SIZE = 6;      // 组织最小成员数

    static final int T_PLAIN=0, T_MOUNT=1, T_RIVER=2, T_SEA=3, T_SPIRIT=4;
    static final String[] T_NAME={"平原","山地","河泽","海上","灵脉"};
    static final int G_GRAIN=0,G_IRON=1,G_SALT=2,G_HIDE=3,G_STONE=4, NG=5;
    static final String[] GOOD_NAME={"粮","铁","盐","兽皮","灵石"};

    static class Agent {
        boolean alive;
        int node, age;
        double[] stock = new double[NG];
        double power, prestige, skill, ambition;
        int org=-1, family=-1, sexMale;
        int[] partners = new int[8];
        byte[] pcount = new byte[8];
        int np=0, tradeCount=0;

        int idx(int other){ for(int i=0;i<np;i++) if(partners[i]==other) return i; return -1; }
        void addTrust(int other){
            int i=idx(other);
            if(i>=0){ if(pcount[i]<100) pcount[i]++; return; }
            if(np<8){ partners[np]=other; pcount[np]=1; np++; }
            else { int m=0; for(int k=1;k<np;k++) if(pcount[k]<pcount[m]) m=k;
                   partners[m]=other; pcount[m]=1; }
        }
        int maxTrust(){ int m=0; for(int k=0;k<np;k++) if(pcount[k]>m) m=pcount[k]; return m; }
    }

    static class Org {
        boolean alive=true;
        String type; int id, leader, bornYear, diedYear;
        Set<Integer> members=new HashSet<>(), nodes=new HashSet<>();
        double pooledGrain, pooledStone;
        boolean teleport; double ambition;
        String deathReason;
        int raids=0, lastSplit=-99;
    }

    static Agent[] A = new Agent[POP_CAP];
    static List<Org> ORGS = new ArrayList<>();
    static ArrayList<Integer>[] nodeAgents;

    static int[] nodeType = new int[NODES];
    static double[] nodeFert=new double[NODES], nodeIron=new double[NODES], nodeSalt=new double[NODES],
                     nodeHide=new double[NODES], nodeStone=new double[NODES];
    static int[] nodeOwner = new int[NODES];
    static double[] nodeTradeHeat = new double[NODES], nodeRaidHeat = new double[NODES];

    static double[] acceptance = new double[NG];
    static int moneyYear=-1, moneyGood=-1;

    static Map<String,Integer> typeEverCount = new TreeMap<>();
    static int totalSplits=0,totalMerges=0,totalOrgDeaths=0,totalRaids=0,totalTrades=0,collapseCount=0;
    static int firstOrgYear=-1, firstFamilyYear=-1, firstEmpireYear=-1, firstCityYear=-1;
    static int[] popByYear=new int[YEARS+1], orgByYear=new int[YEARS+1], maxNodesByYear=new int[YEARS+1];
    static int[] prevOwner=new int[NODES]; static int totalChurn=0, maxChurn=0;
    static List<String> timeline=new ArrayList<>();
    static int families=0, nextFamily=10000;
    static Random R = new Random(1);
    static int[] freeSlots = new int[POP_CAP];
    static int freeTop = 0, aliveCount = 0;

    public static void main(String[] args) {
        int seed = args.length>0? Integer.parseInt(args[0]) : 42;
        long t=System.currentTimeMillis();
        init(seed);
        for(int y=1;y<=YEARS;y++) step(y);
        long ms=System.currentTimeMillis()-t;
        report(ms);
    }

    @SuppressWarnings("unchecked")
    static void init(int seed){
        R = new Random(seed*31L+7);
        for(int n=0;n<NODES;n++){
            double roll=R.nextDouble(); int t=T_PLAIN;
            if(roll<0.30) t=T_MOUNT; else if(roll<0.55) t=T_RIVER;
            else if(roll<0.68) t=T_SEA; else if(roll<0.78) t=T_SPIRIT;
            nodeType[n]=t;
            nodeFert[n]= t==T_PLAIN?1.5: t==T_RIVER?1.9: t==T_MOUNT?0.7: t==T_SEA?0.35:1.0;
            nodeIron[n]= t==T_MOUNT?1.1:0.35;
            nodeSalt[n]= t==T_SEA?1.5: t==T_RIVER?0.8:0.2;
            nodeHide[n]= t==T_MOUNT?1.4:0.5;
            nodeStone[n]= t==T_SPIRIT?1.5:0.12;
            nodeOwner[n]=-1; nodeTradeHeat[n]=0; nodeRaidHeat[n]=0;
        }
        for(int i=0;i<POP_CAP;i++) A[i]=null;
        nodeAgents = new ArrayList[NODES];
        for(int i=0;i<INIT_POP;i++){
            int n=R.nextInt(NODES);
            A[i]=newAgent(n,true);
            aliveCount++;
        }
        freeTop=0;
    }

    static Agent newAgent(int node, boolean adult){
        Agent a=new Agent(); a.alive=true; a.node=node;
        a.age = adult? 16+R.nextInt(22) : 0;
        a.power = Math.max(0.15, 0.6+R.nextGaussian()*0.28);
        a.skill = R.nextDouble();
        a.prestige = a.power*10;
        a.ambition = 0.25+R.nextDouble()*0.75;
        a.sexMale = R.nextBoolean()?1:0;
        for(int g=0;g<NG;g++) a.stock[g]= adult? 1.5+R.nextDouble()*3 : 0.8;
        return a;
    }

    static int allocSlot(int node, boolean adult){
        while(freeTop>0){
            int s=freeSlots[--freeTop];
            if(A[s]==null || !A[s].alive){ A[s]=newAgent(node,adult); aliveCount++; return s; }
        }
        return -1;
    }

    static void freeSlot(int i){ if(freeTop<POP_CAP) freeSlots[freeTop++]=i; }

    // ================= 主循环 =================
    static void step(int year){
        // 重建节点索引
        for(int n=0;n<NODES;n++) nodeAgents[n]=new ArrayList<>(256);
        for(int i=0;i<POP_CAP;i++){ Agent a=A[i]; if(a!=null&&a.alive) nodeAgents[a.node].add(i); }

        // 1) 生产 / 消耗 / 老化
        for(int n=0;n<NODES;n++){
            double f=nodeFert[n], ir=nodeIron[n], sa=nodeSalt[n], hi=nodeHide[n], st=nodeStone[n];
            for(int idx: nodeAgents[n]){
                Agent a=A[idx];
                double labor=(1.0+a.skill*0.5)*(a.age<15?0.35:1.0)*(a.age>60?0.6:1.0);
                a.stock[G_GRAIN]+=f*1.55*labor*(0.85+R.nextDouble()*0.3);
                a.stock[G_IRON] +=ir*0.28*labor;
                a.stock[G_SALT] +=sa*0.18*labor;
                a.stock[G_HIDE] +=hi*0.18*labor;
                a.stock[G_STONE]+=st*0.10*labor;
                a.stock[G_GRAIN]-=0.85+(a.power>1.3?0.35:0);
                a.stock[G_SALT] -=0.08;
                a.stock[G_HIDE] -=0.04;
                a.stock[G_IRON] -=0.035;
                a.stock[G_STONE]-=a.power>1.4?0.16:0.02;
                a.age++;
                a.power *= (a.age<32? 1.010 : 0.994);
            }
        }

        // 2) 死亡
        for(int n=0;n<NODES;n++){
            for(int idx: nodeAgents[n]){
                Agent a=A[idx];
                double hazard=0.006;
                if(a.age>50) hazard += (a.age-50)*0.0032;
                if(a.stock[G_GRAIN]<-1.0) hazard += 0.12;
                if(R.nextDouble()<hazard) die(idx, a.stock[G_GRAIN]<-1.0?"饥荒/寿元":"寿元");
            }
        }
        int pop=0; for(int i=0;i<POP_CAP;i++) if(A[i]!=null&&A[i].alive) pop++;
        popByYear[year]=pop;

        // 3) 交易撮合：每节点分层——中间商(broker)做枢纽 → 形成核心-边缘
        for(int n=0;n<NODES;n++){
            List<Integer> list=nodeAgents[n];
            if(list.size()<4) continue;
            final List<Integer> sorted=new ArrayList<>(list);
            sorted.sort((x,y)->Double.compare(A[y].prestige, A[x].prestige));
            int bcount = effectiveControl(n)>=0 ? Math.min(8, Math.max(2, list.size()/18)) : Math.min(2, Math.max(1, list.size()/50));
            List<Integer> br=new ArrayList<>();
            for(int i=0;i<bcount && i<sorted.size();i++) br.add(sorted.get(i));
            int trades=0;
            for(int idx: list){
                if(br.contains(idx)) continue;
                Agent a=A[idx];
                int g=bestGood(a);
                if(g>=0 && a.stock[g]-1.0>0.10){
                    int b=br.get(R.nextInt(br.size())); Agent B=A[b];
                    double vol=Math.min(a.stock[g]-1.0,1.3);
                    a.stock[g]-=vol; B.stock[g]+=vol*0.97;
                    a.addTrust(b); B.addTrust(idx); a.tradeCount++; B.tradeCount++; trades++;
                } else {
                    int wg=worstGood(a);
                    if(a.stock[wg]<0.5){
                        int b=br.get(R.nextInt(br.size())); Agent B=A[b];
                        if(B.stock[wg]>0.9){
                            double vol=Math.min(B.stock[wg]-0.9,0.8);
                            B.stock[wg]-=vol; a.stock[wg]+=vol*0.97;
                            a.addTrust(b); B.addTrust(idx); a.tradeCount++; B.tradeCount++; trades++;
                        }
                    }
                }
            }
            totalTrades+=trades;
            nodeTradeHeat[n]=nodeTradeHeat[n]*0.97 + trades*0.12;
        }

        // 4) 货币涌现（中介商品正反馈；随后灵石挤出）
        if(moneyYear<0 && year>25){
            double best=-1; int bg=-1;
            for(int g=0;g<NG;g++){ double v=liquidity(g); if(v>best){best=v;bg=g;} }
            if(bg>=0){
                acceptance[bg]+=0.0045+Math.min(best,3.0)*0.0012;
                if(acceptance[bg]>0.50){
                    moneyYear=year; moneyGood=bg;
                    timeline.add("Y"+year+" 一般等价物出现："+GOOD_NAME[bg]+"（此前无任何预置货币）");
                }
            }
        } else if(moneyYear>=0 && moneyGood!=G_STONE && year>moneyYear+30){
            if(liquidity(G_STONE) > liquidity(moneyGood)*1.25){
                moneyGood=G_STONE;
                timeline.add("Y"+year+" 灵石挤出旧币货，成为修士硬通货（兑换率随灵脉丰枯浮动）");
            }
        }

        // 5) 家庭（稳定互惠的一对 → 抚养）
        if(year>=4){
            for(int n=0;n<NODES;n++){
                List<Integer> list=nodeAgents[n];
                for(int k=0;k<list.size();k++){
                    int ai=list.get(k); Agent a=A[ai];
                    if(a.family>=0||a.age<16||a.maxTrust()<TRUST_ORG) continue;
                    for(int p=0;p<a.np;p++){
                        int bi=a.partners[p]; Agent b=A[bi];
                        if(!b.alive||b.family>=0||b.node!=a.node||b.age<16) continue;
                        if(a.pcount[p]<TRUST_ORG) continue;
                        if(R.nextDouble()<0.5){
                            a.family=nextFamily++; b.family=a.family; families++;
                            if(firstFamilyYear<0){ firstFamilyYear=year;
                                timeline.add("Y"+year+" 第一个家庭自发出现（此前世界只有散人）"); }
                        }
                    }
                }
            }
        }

        // 6) 出生（ROI 决定）
        for(int n=0;n<NODES;n++){
            List<Integer> list=nodeAgents[n]; if(list.isEmpty()) continue;
            double grain=0; for(int idx: list) grain+=A[idx].stock[G_GRAIN];
            double perCap=grain/list.size();
            double rate= perCap>2.0?0.020: perCap>1.2?0.012: perCap>0.6?0.005:0.001;
            int births=(int)(list.size()*rate*(0.8+R.nextDouble()*0.5));
            for(int b=0;b<births;b++) if(allocSlot(n,false)<0) break;
        }

        // 7) 群体 → 组织（信任连通分量）
        int[] uf=new int[POP_CAP];
        for(int i=0;i<POP_CAP;i++) uf[i]=i;
        for(int i=0;i<POP_CAP;i++){
            Agent a=A[i]; if(a==null||!a.alive) continue;
            for(int p=0;p<a.np;p++) if(a.pcount[p]>=TRUST_ORG && A[a.partners[p]]!=null && A[a.partners[p]].alive)
                union(uf,i,a.partners[p]);
        }
        Map<Long,List<Integer>> comps=new HashMap<>();
        for(int i=0;i<POP_CAP;i++){
            Agent a=A[i]; if(a==null||!a.alive) continue;
            long key=((long)a.node<<32)|find(uf,i);
            comps.computeIfAbsent(key,k->new ArrayList<>()).add(i);
        }
        for(List<Integer> c: comps.values()){
            if(c.size()<COMP_SIZE) continue;
            int node=A[c.get(0)].node;
            Org exist = nodeOwner[node]>=0? findOrg(nodeOwner[node]) : null;
            if(exist!=null&&exist.alive){
                for(int m:c) if(A[m].org<0){ A[m].org=exist.id; exist.members.add(m); }
                continue;
            }
            int leader=c.get(0);
            for(int m:c) if(A[m].prestige>A[leader].prestige) leader=m;
            Org o=new Org(); o.id=ORGS.size(); o.type=orgType(node,c.size()); o.leader=leader;
            o.bornYear=year; o.ambition=A[leader].ambition;
            for(int m:c){ A[m].org=o.id; o.members.add(m); }
            o.nodes.add(node); nodeOwner[node]=o.id; ORGS.add(o);
            typeEverCount.merge(o.type,1,Integer::sum);
            if(firstOrgYear<0){ firstOrgYear=year;
                timeline.add("Y"+year+" 第一个组织出现："+o.type+"（节点"+node+"，成员"+c.size()+"）"); }
        }

        // 8) 组织演化（用快照，避免迭代中新增组织）
        for(Org o: new ArrayList<>(ORGS)){
            if(!o.alive) continue;
            Agent L=A[o.leader];
            if(L==null||!L.alive){
                int best=-1; double bp=0;
                for(int m:o.members) if(A[m]!=null&&A[m].alive&&A[m].prestige>bp){bp=A[m].prestige;best=m;}
                if(best<0){ killOrg(o,year,"首领身死且无继承者"); continue; }
                o.leader=best; o.ambition=A[best].ambition*0.9;
            }
            o.pooledGrain=0; o.pooledStone=0;
            for(int m:o.members) if(A[m]!=null&&A[m].alive){ o.pooledGrain+=A[m].stock[G_GRAIN]*0.2; o.pooledStone+=A[m].stock[G_STONE]*0.2; }
            if(!o.teleport && o.nodes.size()>=2 && o.pooledStone>55 && year>45){
                o.teleport=true;
                timeline.add("Y"+year+" 组织#"+o.id+"("+o.type+") 建成传送阵 → 控制半径跃升");
            }
            // 传送阵维护成本：灵石不足 → 失修 → 控制半径崩塌（帝国解体的物理原因）
            if(o.teleport){
                double upkeep = 30 + o.nodes.size()*7;
                if(o.pooledStone < upkeep){
                    o.teleport=false; collapseCount++; o.lastSplit=-99;
                    timeline.add("Y"+year+" 组织#"+o.id+"("+o.type+") 传送阵失修 → 控制半径崩塌，开始解体");
                } else {
                    // 扣维护费
                    double per = upkeep/ Math.max(1, o.members.size());
                    for(int m:o.members) if(A[m]!=null&&A[m].alive) A[m].stock[G_STONE]-=per;
                }
            }
            int maxNodes=o.teleport?14:3;
            if(o.nodes.size() > maxNodes*1.3 && year - o.lastSplit > 6){
                int keep=(int)(maxNodes*0.7);
                Set<Integer> sp=new HashSet<>();
                int cnt=0;
                for(int n:o.nodes){ if(cnt++>=keep) sp.add(n); }
                if(!sp.isEmpty()){
                    Org no=new Org(); no.id=ORGS.size(); no.type=o.type; no.bornYear=year;
                    no.leader=o.leader; no.ambition=0.45+R.nextDouble()*0.6;
                    for(int n:sp){
                        no.nodes.add(n); o.nodes.remove(n); nodeOwner[n]=no.id;
                        for(int idx: nodeAgents[n]){ Agent aa=A[idx];
                            if(aa.org==o.id){ aa.org=no.id; o.members.remove(idx); no.members.add(idx); } }
                    }
                    ORGS.add(no); totalSplits++; o.lastSplit=year;
                    timeline.add("Y"+year+" 组织#"+o.id+" 过度扩张分裂 → 新组织#"+no.id+"（资源不足）");
                }
            }
            // 扩张
            if(o.ambition>0.58 && o.nodes.size()<maxNodes && R.nextDouble()<0.40){
                int target=-1;
                for(int n:o.nodes){ int nb=freeNeighbor(n); if(nb>=0){ target=nb; break; } }
                if(target>=0){
                    o.nodes.add(target); nodeOwner[target]=o.id;
                    for(int idx: nodeAgents[target]){ Agent aa=A[idx];
                        if(aa.org>=0){ Org old=findOrg(aa.org); if(old!=null&&old.alive) old.members.remove(idx); }
                        aa.org=o.id; o.members.add(idx); }
                    maybeType(o,year);
                }
            }
            // 吞并
            if(o.ambition>0.72 && R.nextDouble()<0.22){
                for(int n: new ArrayList<>(o.nodes)){
                    for(int nb: neighbors(n)){
                        if(nodeOwner[nb]<0) continue;
                        Org t=findOrg(nodeOwner[nb]);
                        if(t==null||!t.alive||t.id==o.id) continue;
                        if(t.members.size() < Math.max(3,o.members.size()/4)){
                            for(int m:t.members) if(A[m]!=null&&A[m].alive){ A[m].org=o.id; o.members.add(m); }
                            for(int nn:t.nodes){ o.nodes.add(nn); nodeOwner[nn]=o.id; }
                            killOrg(t,year,"被组织#"+o.id+" 吞并");
                            totalMerges++; maybeType(o,year);
                            timeline.add("Y"+year+" 组织#"+o.id+" 吞并组织#"+t.id);
                            break;
                        }
                    }
                }
            }
        }
        // 清理 + 空组织死亡
        for(Org o: ORGS){
            if(!o.alive) continue;
            o.members.removeIf(m-> A[m]==null||!A[m].alive);
            if(o.members.isEmpty()) killOrg(o,year,"成员耗尽");
        }

        // 9) 掠夺 vs 交易（无执法区劫掠）
        for(int n=0;n<NODES;n++){
            List<Integer> list=nodeAgents[n]; if(list.size()<3) continue;
            boolean enforced=effectiveControl(n)>=0;
            int strong=-1; double mp=0;
            for(int idx:list) if(A[idx].power>mp){mp=A[idx].power;strong=idx;}
            if(!enforced && mp>1.25 && R.nextDouble()<0.5){
                int victim=list.get(R.nextInt(list.size()));
                if(victim!=strong){
                    Agent s=A[strong], v=A[victim];
                    double steal=Math.max(0,v.stock[G_GRAIN]*0.5);
                    v.stock[G_GRAIN]-=steal; s.stock[G_GRAIN]+=steal*0.9;
                    totalRaids++; nodeRaidHeat[n]+=1;
                    Org so=findOrg(s.org); if(so!=null) so.raids++;
                    if(R.nextDouble()<0.04) die(victim,"被劫杀");
                }
            }
            nodeRaidHeat[n]*=0.97;
        }

        int churn=0;
        for(int n=0;n<NODES;n++){ if(prevOwner[n]!=nodeOwner[n]) churn++; prevOwner[n]=nodeOwner[n]; }
        totalChurn+=churn; if(churn>maxChurn) maxChurn=churn;

        int ao=0,mn=0;
        for(Org o:ORGS) if(o.alive){ ao++; mn=Math.max(mn,o.nodes.size()); }
        orgByYear[year]=ao; maxNodesByYear[year]=mn;
    }

    // ---------- 工具 ----------
    static int bestGood(Agent a){ int g=-1; double v=1.2; for(int i=0;i<NG;i++) if(a.stock[i]>v){v=a.stock[i];g=i;} return g; }
    static int worstGood(Agent a){ int g=0; double v=1e9; for(int i=0;i<NG;i++) if(a.stock[i]<v){v=a.stock[i];g=i;} return g; }
    static double liquidity(int g){ double s=0; for(int i=0;i<POP_CAP;i++){Agent a=A[i]; if(a!=null&&a.alive) s+=Math.min(a.stock[g],2);} return s/1500.0; }
    static int find(int[] uf,int x){ while(uf[x]!=x){uf[x]=uf[uf[x]];x=uf[x];} return x; }
    static void union(int[] uf,int a,int b){ int ra=find(uf,a),rb=find(uf,b); if(ra!=rb) uf[ra]=rb; }
    static Org findOrg(int id){ return id>=0&&id<ORGS.size()?ORGS.get(id):null; }
    static int[] neighbors(int n){
        int x=n%NODE_W,y=n/NODE_W; int[] res=new int[4]; int c=0;
        if(x>0) res[c++]=n-1; if(x<NODE_W-1) res[c++]=n+1;
        if(y>0) res[c++]=n-NODE_W; if(y<NODE_W-1) res[c++]=n+NODE_W;
        return Arrays.copyOf(res,c);
    }
    static int effectiveControl(int n){
        int oid=nodeOwner[n];
        if(oid<0) return -1;
        Org o=findOrg(oid);
        if(o==null||!o.alive) return -1;
        int cap=Integer.MAX_VALUE;
        for(int x:o.nodes) if(x<cap) cap=x;
        int radius=o.teleport?6:1;
        int d=Math.abs(n%NODE_W-cap%NODE_W)+Math.abs(n/NODE_W-cap/NODE_W);
        return d<=radius? oid : -1;
    }
    static void maybeType(Org o,int year){
        if(o.nodes.size()>=8 && !"帝国".equals(o.type)){
            o.type="帝国"; typeEverCount.merge("帝国",1,Integer::sum);
            if(firstEmpireYear<0){ firstEmpireYear=year;
                timeline.add("Y"+year+" 第一个帝国成形（组织#"+o.id+"，节点"+o.nodes.size()+"）"); }
        } else if(o.nodes.size()>=3 && "部落".equals(o.type)){
            o.type="城邦"; typeEverCount.merge("城邦",1,Integer::sum);
            if(firstCityYear<0){ firstCityYear=year;
                timeline.add("Y"+year+" 第一个城邦出现（组织#"+o.id+"）"); }
        }
    }
    static int freeNeighbor(int n){
        for(int nb: neighbors(n)) if(nodeOwner[nb]<0) return nb;
        return -1;
    }
    static String orgType(int node,int size){
        int t=nodeType[node];
        if(families>0 && size>=7 && R.nextDouble()<0.20) return "家族";
        if(t==T_SPIRIT) return "宗门";
        if(t==T_SEA) return "商帮";
        if(t==T_PLAIN&&size>=10) return "城邦";
        return "部落";
    }
    static void die(int idx,String reason){
        Agent a=A[idx]; if(a==null||!a.alive) return;
        a.alive=false; aliveCount--; freeSlot(idx);
        Org o=findOrg(a.org); if(o!=null) o.members.remove(idx);
    }
    static void killOrg(Org o,int year,String reason){
        if(!o.alive) return;
        o.alive=false; o.diedYear=year; o.deathReason=reason; totalOrgDeaths++;
        for(int n:o.nodes) if(nodeOwner[n]==o.id) nodeOwner[n]=-1;
        for(int m:o.members) if(A[m]!=null) A[m].org=-1;
        timeline.add("Y"+year+" 组织#"+o.id+"("+o.type+") 灭亡："+reason);
    }

    // ---------- 报告 ----------
    static void report(long msMs){
        System.out.println("================ P0 无头世界模拟 · 结果 ================");
        System.out.printf("规模：%d 节点 / 开局 %d 散人 / 跑 %d 年 / 耗时 %.2f 秒%n", NODES,INIT_POP,YEARS,msMs/1000.0);
        System.out.println("---- 时间线 ----");
        int shown=0;
        for(String s:timeline){ if(shown++<14) System.out.println("  · "+s); }
        System.out.println("  ... 共 "+timeline.size()+" 条事件");

        int alive=0; for(int i=0;i<POP_CAP;i++) if(A[i]!=null&&A[i].alive) alive++;
        int aliveOrgs=0; double mx=0; Map<String,Integer> types=new TreeMap<>();
        for(Org o:ORGS) if(o.alive){ aliveOrgs++; mx=Math.max(mx,o.nodes.size()); types.merge(o.type,1,Integer::sum); }

        System.out.println("\n---- 验收对照 ----");
        System.out.printf("① 终局组织类型 %d 种 %s ；历史累计 %d 种 %s%n", types.size(),types,typeEverCount.size(),typeEverCount);
        System.out.printf("② 分裂 %d 次 / 合并 %d 次%n", totalSplits,totalMerges);
        System.out.printf("③ 组织灭亡 %d 次%n", totalOrgDeaths);
        System.out.printf("④ 首个组织出现于 Y%d（开局 0 组织）%n", firstOrgYear);
        System.out.printf("⑤ 交易 %d 笔；集中度（活跃者前5%%占比）%.1f%%%n", totalTrades, concentration());
        int ord=0,wild=0;
        for(int n=0;n<NODES;n++){
            if(effectiveControl(n)>=0 && nodeTradeHeat[n]>=nodeRaidHeat[n]*2) ord++;
            else if(effectiveControl(n)<0) wild++;
        }
        System.out.printf("⑥ 秩序区 %d/%d (%.0f%%) · 荒野区 %d/%d (%.0f%%) ｜ 每年平均易主 %.1f 节点(峰值 %d)%n",
            ord,NODES,100.0*ord/NODES, wild,NODES,100.0*wild/NODES, (double)totalChurn/YEARS, maxChurn);
        System.out.printf("⑦ 一般等价物：%s ｜ 劫掠事件 %d 次 ｜ 传送阵崩塌(帝国解体) %d 次%n",
            moneyYear<0? "未出现（不合格）" : "Y"+moneyYear+" → "+GOOD_NAME[moneyGood], totalRaids, collapseCount);
        System.out.printf("⑧ 首个家庭 Y%d；家庭数 %d%n", firstFamilyYear, families);
        System.out.printf("⑨ 势力峰值 %.0f 节点（帝国门槛 8）｜首个城邦 Y%d｜首个帝国 Y%d%n", mx,firstCityYear,firstEmpireYear);
        System.out.printf("\n终局：存活个体 %d · 存活组织 %d%n", alive, aliveOrgs);
        StringBuilder sb=new StringBuilder("  人口(每50年): ");
        for(int y=50;y<=YEARS;y+=50) sb.append(y).append(":").append(popByYear[y]).append(" ");
        System.out.println(sb);
        sb=new StringBuilder("  最大势力(每50年): ");
        for(int y=50;y<=YEARS;y+=50) sb.append(y).append(":").append(maxNodesByYear[y]).append(" ");
        System.out.println(sb);
        System.out.println("==================================================");
    }

    static double concentration(){
        Integer[] c=new Integer[POP_CAP]; int n=0,total=0;
        for(int i=0;i<POP_CAP;i++){ Agent a=A[i]; if(a!=null&&a.tradeCount>0){ c[n++]=a.tradeCount; total+=a.tradeCount; } }
        if(total==0) return 0;
        int k=Math.max(1,(int)(n*0.05));
        Arrays.sort(c,0,n,Collections.reverseOrder());
        int top=0; for(int i=0;i<k;i++) top+=c[i];
        return 100.0*top/total;
    }
}
