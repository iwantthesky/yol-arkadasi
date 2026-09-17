using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace DortCuce.UnityGame
{
    /// <summary>Single authoritative simulation; every remote player contributes only their assigned controls.</summary>
    public sealed class MotorCoopGame : MonoBehaviour
    {
        private enum Page { Home, Lobby, Ride }
        private Page page;
        private CoopSession session;
        private MotorSimulation simulation;
        private MotorWorld world;
        private MotorBikeView bike;
        private MotorSound sound;
        private Camera follow;
        private BikeSnapshot state;
        private MotorInput input, combined;
        private string playerName="Engin",address="192.168.1.100";
        private int capacity=4,rosterRevision,cameraMode;
        private bool paused,help=true,wasClient;
        private float clutch,sceneTime,qaStart,lastBotShift=-10;
        private string qaMode,qaFolder;
        private int qaPort=47777;
        private float qaSeconds=22;
        private bool qaSaved,qaCapture;
        private int maxPlayers,qaErrors;
        private string videoFramesFolder;
        private int videoFps=24,videoWidth=1280,videoHeight=720,videoFramesWritten,videoFrameTarget;
        private float balanceDemoCrashAt=-1f,balanceDemoRestartAt=-1f,balanceDemoRecoveredAt=-1f;
        private bool balanceDemoFailed,balanceDemoRecovered;
        private GUIStyle body,big,title,small,button,field;
        private Texture2D panelTexture;
        private static readonly Color Ink=new(.035f,.09f,.105f,.94f);
        private static readonly Color Cream=new(.96f,.92f,.82f,1);
        private static readonly Color Muted=new(.58f,.71f,.69f,1);
        private static readonly Color Gold=new(.99f,.69f,.26f,1);
        private static readonly Color Teal=new(.30f,.77f,.67f,1);
        public CoopSession Session=>session;
        public BikeSnapshot State=>state;
        public bool Playing=>page==Page.Ride&&!paused;

        private void Awake()
        {
            Application.runInBackground=true; Application.targetFrameRate=60;
            QualitySettings.vSyncCount=0; QualitySettings.antiAliasing=4;
            Time.fixedDeltaTime=1f/60;
            session=gameObject.AddComponent<CoopSession>();session.Solo();
            simulation=new MotorSimulation();state=simulation.State;
            world=new GameObject("900 metre • Son Durak").AddComponent<MotorWorld>();world.Build();
            bike=new GameObject("Engin Motor").AddComponent<MotorBikeView>();bike.Build();
            bike.transform.position=MotorWorld.Point(0);
            follow=new GameObject("Yol kamerası").AddComponent<Camera>();
            follow.tag="MainCamera";follow.nearClipPlane=.08f;follow.farClipPlane=650;follow.fieldOfView=58;
            follow.backgroundColor=new(.64f,.75f,.72f);follow.clearFlags=CameraClearFlags.Skybox;
            follow.gameObject.AddComponent<AudioListener>();
            sound=gameObject.AddComponent<MotorSound>();
            playerName=PlayerPrefs.GetString("motor.player.v2","Engin");
            Application.logMessageReceived+=OnLog;
            ReadQaArguments();
        }

        private void Start()
        {
            if(string.IsNullOrEmpty(videoFramesFolder))return;
            Directory.CreateDirectory(videoFramesFolder);
            videoFrameTarget=Mathf.Max(1,Mathf.RoundToInt(qaSeconds*videoFps));
            Time.captureFramerate=videoFps;
            StartCoroutine(CaptureVideoFrames());
        }
        private void OnLog(string message,string stack,LogType type)
        {if(type==LogType.Exception||type==LogType.Error)qaErrors++;}

        private void ReadQaArguments()
        {
            foreach(var arg in Environment.GetCommandLineArgs())
            {
                if(arg.StartsWith("--qa="))qaMode=arg.Substring(5);
                if(arg.StartsWith("--qa-out="))qaFolder=arg.Substring(9);
                if(arg.StartsWith("--qa-seconds="))float.TryParse(arg.Substring(13),out qaSeconds);
                if(arg.StartsWith("--qa-port="))int.TryParse(arg.Substring(10),out qaPort);
                if(arg.StartsWith("--qa-name="))playerName=arg.Substring(10);
                if(arg.StartsWith("--qa-address="))address=arg.Substring(13);
                if(arg=="--qa-capture")qaCapture=true;
                if(arg.StartsWith("--video-frames="))videoFramesFolder=arg.Substring(15);
                if(arg.StartsWith("--video-fps="))int.TryParse(arg.Substring(12),out videoFps);
                if(arg.StartsWith("--video-width="))int.TryParse(arg.Substring(14),out videoWidth);
                if(arg.StartsWith("--video-height="))int.TryParse(arg.Substring(15),out videoHeight);
            }
            videoFps=Mathf.Clamp(videoFps,12,60);videoWidth=Mathf.Clamp(videoWidth,640,1920);videoHeight=Mathf.Clamp(videoHeight,360,1080);
            if(string.IsNullOrEmpty(qaMode))return;
            qaStart=Time.realtimeSinceStartup;
            if(qaMode=="host"){session.Host(playerName,4,qaPort);page=Page.Lobby;}
            else if(qaMode=="client"){session.Join(address,playerName,qaPort);page=Page.Lobby;}
            else if(qaMode=="solo"||qaMode=="freeroam"||qaMode=="balance-demo"){page=Page.Ride;}
        }

        private void Update()
        {
            sceneTime+=Time.deltaTime;
            if(wasClient&&!session.IsClient){page=Page.Lobby;paused=false;}
            wasClient=session.IsClient;
            if(session.IsClient)
            {
                if(session.RemoteState!=null)state=session.RemoteState;
                if(session.IsConnected&&session.RemotePlaying){if(page!=Page.Ride)paused=false;page=Page.Ride;}
                else if(page==Page.Ride){page=Page.Lobby;}
            }
            if(session.IsHost&&session.RosterRevision!=rosterRevision)
            {if(page==Page.Ride)paused=true;rosterRevision=session.RosterRevision;}
            if(Input.GetKeyDown(KeyCode.Escape)&&page==Page.Ride)paused=!paused;
            if(Input.GetKeyDown(KeyCode.H))help=!help;
            if(Input.GetKeyDown(KeyCode.C))cameraMode=(cameraMode+1)%2;
            if(Input.GetKeyDown(KeyCode.M))sound.Muted=!sound.Muted;
            if(!string.IsNullOrEmpty(qaMode))QaUpdate();
            else
            {
                clutch=Mathf.MoveTowards(clutch,Input.GetKey(KeyCode.Space)?1:0,Time.deltaTime*(Input.GetKey(KeyCode.Space)?8:1.6f));
                input=new MotorInput{
                    throttle=Input.GetKey(KeyCode.W)?1:0,brake=Input.GetKey(KeyCode.S)?1:0,
                    steer=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),
                    balance=(Input.GetKey(KeyCode.RightArrow)?1:0)-(Input.GetKey(KeyCode.LeftArrow)?1:0),
                    clutch=clutch,shift=(Input.GetKeyDown(KeyCode.E)?1:0)-(Input.GetKeyDown(KeyCode.Q)?1:0),
                    ignition=Input.GetKeyDown(KeyCode.I),reset=Input.GetKeyDown(KeyCode.R)};
            }
            if(page!=Page.Ride||paused)input=default;
            session.SubmitLocal(input);
            maxPlayers=Mathf.Max(maxPlayers,session.PlayerCount);
            if(!session.IsClient)session.Publish(simulation.State,page==Page.Ride&&!paused);
            world.Animate(state.elapsed);
            sound.Apply(state,page==Page.Ride&&!paused);
        }

        private void FixedUpdate()
        {
            if(session.IsClient)return;
            combined=session.CombinedInput();
            if(page==Page.Ride&&!paused)simulation.Step(combined,Time.fixedDeltaTime);
            state=simulation.State;
        }

        private void LateUpdate()
        {
            bike.Animate(state,session.IsClient?input:combined,Time.deltaTime);
            Vector3 cameraPosition,look;
            if(page==Page.Home)
            {
                var orbit=sceneTime*.035f;
                cameraPosition=bike.transform.position+new Vector3(5.4f+Mathf.Sin(orbit)*.6f,2.8f,4.5f);
                look=bike.transform.position+new Vector3(-1.0f,.95f,0);
                follow.fieldOfView=50;
            }
            else
            {
                var p=bike.transform.position;
                var forward=new Vector3(Mathf.Sin(state.heading),0,Mathf.Cos(state.heading));
                var right=new Vector3(forward.z,0,-forward.x);
                bool balanceShot=qaMode=="balance-demo";
                float back=balanceShot?5.8f:(cameraMode==0?7.2f:3.7f);
                float side=balanceShot?3.1f:(cameraMode==0?2.2f:0);
                float height=balanceShot?2.65f:(cameraMode==0?3.4f:1.9f);
                cameraPosition=p-forward*back+right*side+Vector3.up*height;
                float ground=MotorCourse.GroundHeight(cameraPosition.z,cameraPosition.x);
                cameraPosition.y=Mathf.Max(cameraPosition.y,ground+2f);
                look=p+forward*(balanceShot?3.0f:4.8f)+Vector3.up*(balanceShot?.9f:1f);
                float targetFov=(balanceShot?52f:58f)+state.speed*.30f;
                follow.fieldOfView=Mathf.Lerp(follow.fieldOfView,targetFov,Time.deltaTime*2);
            }
            follow.transform.position=Vector3.Lerp(follow.transform.position,cameraPosition,1-Mathf.Exp(-6*Time.deltaTime));
            follow.transform.rotation=Quaternion.Slerp(follow.transform.rotation,Quaternion.LookRotation(look-follow.transform.position),1-Mathf.Exp(-8*Time.deltaTime));
        }

        private void StartRide(bool solo)
        {
            if(solo)session.Solo();
            simulation.ResetRun();state=simulation.State;paused=false;page=Page.Ride;clutch=0;
            if(string.IsNullOrEmpty(qaMode))
            {PlayerPrefs.SetString("motor.player.v2",playerName);PlayerPrefs.Save();}
        }

        private void Styles()
        {
            if(body!=null)return;
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            body=new GUIStyle{font=font,fontSize=20,normal={textColor=Cream},wordWrap=true};
            small=new GUIStyle(body){fontSize=14,normal={textColor=Muted}};
            title=new GUIStyle(body){fontSize=61,fontStyle=FontStyle.Bold,wordWrap=false};
            big=new GUIStyle(body){fontSize=42,fontStyle=FontStyle.Bold};
            button=new GUIStyle(body){alignment=TextAnchor.MiddleCenter,fontSize=19,fontStyle=FontStyle.Bold};
            field=new GUIStyle(body){fontSize=20,padding=new RectOffset(14,10,12,5),normal={textColor=Cream,background=Texture2D.whiteTexture}};
            panelTexture=Texture2D.whiteTexture;
        }
        private void Box(float x,float y,float w,float h,Color color)
        {var c=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(x,y,w,h),panelTexture);GUI.color=c;}
        private void Text(float x,float y,float w,float h,string value,GUIStyle style=null,Color? color=null)
        {var use=style??body;var old=use.normal.textColor;if(color.HasValue)use.normal.textColor=color.Value;GUI.Label(new Rect(x,y,w,h),value,use);use.normal.textColor=old;}
        private bool Button(float x,float y,float w,float h,string label,bool primary=false,bool enabled=true)
        {
            var r=new Rect(x,y,w,h);bool hover=r.Contains(Event.current.mousePosition);
            Box(x,y,w,h,primary?(enabled?(hover?new Color(1,.77f,.4f):Gold):new Color(.3f,.35f,.32f)):(hover?new Color(.16f,.30f,.31f):new Color(.10f,.23f,.25f)));
            button.normal.textColor=primary?Ink:Cream;bool previous=GUI.enabled;GUI.enabled=enabled;
            bool hit=GUI.Button(r,label,button);GUI.enabled=previous;return hit;
        }
        private string Field(float x,float y,float w,string value,int max=32)
        {
            Box(x,y,w,48,new(.08f,.19f,.21f));
            var old=GUI.backgroundColor;GUI.backgroundColor=new(.08f,.19f,.21f);
            value=GUI.TextField(new Rect(x,y,w,48),value,max,field);GUI.backgroundColor=old;return value;
        }

        private void OnGUI()
        {
            Styles();float scale=Mathf.Min(Screen.width/1440f,Screen.height/900f);
            var previous=GUI.matrix;GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1440*scale)/2,(Screen.height-900*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            if(page==Page.Home)Home();else if(page==Page.Lobby)Lobby();else RideHud();
            GUI.matrix=previous;
        }

        private void Home()
        {
            Box(0,0,570,900,Ink);Box(40,49,35,4,Gold);
            Text(89,36,420,40,"DÖRT CÜCE BİR MOTOR  /  CO-OP",small,Gold);
            Text(40,107,600,148,"YOL\nARKADAŞI",title);
            Text(43,267,465,68,"Dört kişi. Tek motor.\nSon durağa birlikte ulaşın.",body);
            Text(43,362,450,26,"TAKMA ADIN",small);playerName=Field(43,393,468,playerName,20);
            Text(43,459,450,25,"EKİP KAPASİTESİ",small);
            for(int i=2;i<=4;i++)if(Button(43+(i-2)*158,493,150,42,i+" KİŞİ",capacity==i))capacity=i;
            if(Button(43,554,468,57,"EKİBİ KUR   →",true))
            {session.Host(string.IsNullOrWhiteSpace(playerName)?"Sürücü":playerName,capacity);page=Page.Lobby;}
            address=Field(43,629,302,address,64);
            if(Button(355,629,156,48,"KATIL")){session.Join(address,string.IsNullOrWhiteSpace(playerName)?"Yolcu":playerName);page=Page.Lobby;}
            Text(43,687,470,40,"Aynı yerel ağdaki veya VPN ağındaki arkadaşının IP adresi.",small);
            if(Button(43,750,468,46,"TEK BAŞINA ANTRENMAN"))StartRide(true);
            Text(43,823,470,48,"900 m rota + serbest harita  •  manuel şanzıman  •  gerçek denge\nMotor: Engin / d.blend  ·  Çevre: Antigravity özgün assetleri",small);
            Box(1040,62,346,100,new(.035f,.09f,.105f,.80f));Text(1064,80,290,25,"ROTA 01 / SON DURAK",small,Gold);
            Text(1064,113,310,40,"Antigravity Dağ Geçidi",body);
            Text(965,817,420,42,"Bir kişi gaz verir. Herkes sorumludur.",small,Cream);
        }

        private void Lobby()
        {
            Box(155,75,1130,747,Ink);
            Text(201,108,910,36,"YOL ARKADAŞI   /   EKİP GARAJI",small,Gold);
            Text(199,151,950,67,session.IsHost?"Ekibini topla.":"Yola birlikte çıkın.",big);
            Text(201,230,1030,52,session.Status,body);
            if(session.IsHost)Text(201,281,1020,44,"Bağlantı adresin: "+CoopSession.LocalAddresses()+"  •  TCP 47777",small,Teal);
            for(int i=0;i<4;i++)
            {
                int y=342+i*69;Box(201,y,1038,58,i==session.LocalSlot?new(.14f,.30f,.30f):new(.07f,.17f,.19f));
                string name=i<session.PlayerNames.Length?session.PlayerNames[i]:"";
                bool occupied=i<session.PlayerCount;
                Text(223,y+15,60,35,(i+1).ToString("00"),body,occupied?Gold:Muted);
                Text(290,y+15,330,35,occupied?name:"Arkadaş bekleniyor…",body,occupied?Cream:Muted);
                Text(676,y+18,540,40,occupied?CoopSession.RoleLabel(i,session.PlayerCount):"",small,Teal);
            }
            Text(201,643,990,45,"Oyuncu sayısı değişince görevler yeniden paylaşılır. Herkes kendi rolündeki tuşları kullanır.",small);
            if(Button(201,726,244,50,"← GERİ")){session.Leave();page=Page.Home;}
            if(session.IsHost)
            {if(Button(734,726,505,50,"BİRLİKTE YOLA ÇIK   →",true,session.PlayerCount>=2))StartRide(false);}
            else Text(730,739,490,42,session.IsConnected?"Ev sahibinin başlatması bekleniyor…":"Bağlantı bekleniyor…",body);
        }

        private void RideHud()
        {
            Box(32,28,330,83,Ink);Text(52,40,300,24,"YOL ARKADAŞI / SON DURAK",small,Gold);
            Text(52,70,302,31,Chapter(state.distance),body);
            Box(465,28,510,73,Ink);Text(485,40,430,30,state.finished?$"SERBEST SÜRÜŞ   •   {state.distance:0} m":$"{Mathf.Clamp(state.distance,0,900):0} / 900 m   •   KAMP {state.checkpoint+1}",small);
            Box(485,77,470,5,new(.20f,.31f,.31f));Box(485,77,470*Mathf.Clamp01(state.distance/900),5,Gold);
            Box(1078,28,330,83,Ink);Text(1098,42,298,24,$"{session.PlayerCount} KİŞİ  •  {state.elapsed/60:00}:{state.elapsed%60:00}",small,Teal);
            Text(1098,73,292,30,session.IsOnline?(session.IsHost?"EKİP LİDERİ":"BAĞLI") : "ANTRENMAN",small);

            Box(32,666,254,198,Ink);
            Text(52,680,180,74,$"{state.speed*3.6f:00}",title);Text(193,729,70,26,"km/sa",small);
            Text(52,764,177,30,$"{state.rpm:0} dev/dk",small,state.engineRunning?Muted:new Color(1,.4f,.3f));
            Box(52,806,204,7,new(.18f,.30f,.30f));Box(52,806,204*Mathf.Clamp01(state.rpm/8500),7,state.rpm>6800?Gold:Teal);
            Text(52,827,204,25,state.engineRunning?"MOTOR ÇALIŞIYOR":"STOP • SPACE + I",small);
            Box(300,666,126,198,Ink);Text(324,682,96,32,"VİTES",small,Gold);
            Text(331,715,100,90,state.gear==0?"N":state.gear.ToString(),title);
            Text(324,826,96,25,"Q  /  E",small);

            Box(449,746,542,118,Ink);Text(471,760,485,27,"DENGE  /  AĞIRLIK AKTARIMI",small,Gold);
            Box(479,811,480,5,new(.26f,.40f,.40f));Box(713,796,8,33,Cream);
            float leanX=719+Mathf.Clamp(state.lean,-1.1f,1.1f)/1.1f*224;
            Box(leanX-5,799,10,28,Mathf.Abs(state.lean)>.6f?new(1,.35f,.25f):Teal);
            Text(479,838,160,23,"← SOL",small);Text(884,838,100,23,"SAĞ →",small);

            Box(1013,666,395,198,Ink);Text(1034,681,350,25,"SENİN GÖREVİN",small,Gold);
            Text(1034,713,350,55,CoopSession.RoleLabel(session.LocalSlot,session.PlayerCount),body);
            Text(1034,779,350,70,ControlsForRole(),small,Teal);
            Text(34,876,1000,22,"ESC duraklat / menü     H rehber     C kamera     M ses     R motoru kaldır / son kampa dön",small);
            if(!string.IsNullOrEmpty(state.message))
            {Box(459,119,522,49,Ink);Text(479,132,480,36,state.message,small,Cream);}
            if(help&&state.distance<65&&!state.crashed)
            {
                Box(34,138,377,219,Ink);Text(55,157,330,27,"İLK KALKIŞ",small,Gold);
                Text(55,197,325,145,"1  SPACE ile debriyajı ayır.\n2  E ile birinci vitese al.\n3  W ile gaz ver, SPACE'i bırak.\n4  A / D gidonu çevirir.\n5  ← / → ile dönüşe doğru ağırlık ver.\nYol dışı dahil bütün harita sürülebilir.",small,Cream);
            }
            if(state.crashed)
            {
                Box(420,271,600,309,Ink);
                Text(455,307,535,63,"BİRLİKTE KALKIN",big,Cream);
                Text(455,383,515,78,state.message,body);
                if(!session.IsClient)
                {if(Button(455,494,530,53,"SON KAMPTAN DEVAM",true))simulation.Respawn();}
                else Text(455,507,535,48,"Ev sahibi devam ettirebilir.",body);
            }
            if(paused)PausePanel();
        }

        private string ControlsForRole()
        {
            var mask=CoopSession.MaskInput(new MotorInput{throttle=1,brake=1,steer=1,balance=1,clutch=1,shift=1,ignition=true},session.LocalSlot,session.PlayerCount);
            string s="";if(mask.throttle>0)s+="W / S  gaz & fren\n";if(mask.steer>0)s+="A / D  direksiyon\n";
            if(mask.clutch>0)s+="SPACE debriyaj · Q/E vites · I marş\n";if(mask.balance>0)s+="← / →  ağırlığı kaydır";return s.Trim();
        }
        private static string Chapter(float d)=>d<0?"SERBEST ALAN":d<180?"01  •  ISINMA & SLALOM":d<360?"02  •  RÜZGÂR VADİSİ":d<540?"03  •  KÖPRÜ & SIÇRAYIŞ":d<720?"04  •  DAĞ GEÇİDİ":d<=900?"05  •  SON VİRAJ":"SERBEST SÜRÜŞ";
        private void PausePanel()
        {
            Box(418,199,604,463,Ink);Text(458,239,510,60,"KISA BİR MOLA",big);
            Text(458,322,510,88,session.IsClient?"Senin girişlerin bekletiliyor. Ekip lideri ortak sürüşü duraklatabilir.":"Yeni biri katıldıysa görevleri kontrol edin. Hazır olduğunuzda birlikte devam edin.",body);
            if(Button(458,432,522,53,"DEVAM ET",true))paused=false;
            if(Button(458,504,522,51,"GARAJA DÖN")){session.Leave();simulation.ResetRun();page=Page.Home;paused=false;}
            if(Button(458,574,522,45,"OYUNDAN ÇIK"))Application.Quit();
        }

        private void QaUpdate()
        {
            float t=string.IsNullOrEmpty(videoFramesFolder)?Time.realtimeSinceStartup-qaStart:videoFramesWritten/(float)videoFps;
            if(qaMode=="host"&&session.PlayerCount>=2&&t>6&&page==Page.Lobby)StartRide(false);
            if(qaMode=="host"&&page==Page.Ride)paused=false;
            input=default;
            if(qaMode=="balance-demo")
            {
                BalanceDemoUpdate(t);
            }
            else if(qaMode!="menu")
            {
                float qaTargetSpeed=qaMode=="freeroam"?7.5f:20f;
                input.throttle=Mathf.Clamp01(.22f+(qaTargetSpeed-state.speed)*.22f);
                input.brake=Mathf.Clamp01((state.speed-qaTargetSpeed-.35f)*.35f);
                float lookDistance=state.distance+18f;
                float worldX=MotorCourse.Sample(state.distance).centerX+state.lateral;
                float desiredHeading=qaMode=="freeroam"&&t>3f?(Mathf.Abs(state.lateral)<38f?48f*Mathf.Deg2Rad:0f):
                    Mathf.Atan2(MotorCourse.Sample(lookDistance).centerX-worldX,lookDistance-state.distance);
                float headingError=Mathf.DeltaAngle(state.heading*Mathf.Rad2Deg,desiredHeading*Mathf.Rad2Deg)*Mathf.Deg2Rad;
                input.steer=qaMode=="freeroam"&&t>3f?Mathf.Clamp(headingError*2f,-1f,1f):
                    Mathf.Clamp(headingError*2.2f-state.lateral*.07f,-1f,1f);
                float turnAcceleration=state.speed*state.speed/1.45f*Mathf.Tan(state.steeringAngle);
                input.balance=Mathf.Clamp(turnAcceleration/(1.05f*8f)-state.lean*1.55f-state.leanVelocity*.42f,-1,1);
                input.clutch=state.gear==0?1:Mathf.Clamp01(1-(state.elapsed-.9f)*.9f);
                if(state.gear==0&&t-lastBotShift>.8f){input.shift=1;lastBotShift=t;}
                if(!state.engineRunning){input.clutch=1;input.ignition=true;}
            }
            bool captureComplete=string.IsNullOrEmpty(videoFramesFolder)?t>qaSeconds:videoFramesWritten>=videoFrameTarget;
            if(captureComplete&&!qaSaved)
            {qaSaved=true;StartCoroutine(SaveQa());}
        }

        private void BalanceDemoUpdate(float t)
        {
            if(balanceDemoRestartAt<0f)
            {
                if(state.crashed)
                {
                    balanceDemoFailed=true;
                    if(balanceDemoCrashAt<0f)balanceDemoCrashAt=t;
                    if(t-balanceDemoCrashAt>2.35f)
                    {
                        simulation.ResetRun();state=simulation.State;
                        balanceDemoRestartAt=t;lastBotShift=t-1f;
                    }
                    return;
                }
                float balance=BalanceCorrection();
                float steer=0f;
                if(t>=4f)
                {
                    // First try: the driver turns right while the balance rider shifts left.
                    // This deliberately reinforces the fall so the failed teamwork is visible.
                    steer=1f;balance=-1f;
                }
                SetQaDrive(t,9f,steer,balance);
                return;
            }

            float phase=t-balanceDemoRestartAt;
            float worldX=MotorCourse.Sample(state.distance).centerX+state.lateral;
            float lookDistance=state.distance+16f;
            float routeHeading=Mathf.Atan2(MotorCourse.Sample(lookDistance).centerX-worldX,lookDistance-state.distance);
            float weaveHeading=phase<4f?0f:
                phase<6.5f?.18f:
                phase<9f?-.18f:
                phase<11.5f?.18f:
                phase<14f?-.18f:0f;
            float desiredHeading=routeHeading+weaveHeading;
            float headingError=Mathf.DeltaAngle(state.heading*Mathf.Rad2Deg,desiredHeading*Mathf.Rad2Deg)*Mathf.Deg2Rad;
            float demoSteer=Mathf.Clamp(headingError*3.2f-state.lateral*.045f,-1f,1f);
            SetQaDrive(t,phase<14f?7.5f:0f,demoSteer,BalanceCorrection());
            if(!state.crashed&&phase>=16f&&state.speed<.6f&&Mathf.Abs(state.lean)<.45f)
            {
                balanceDemoRecovered=true;
                if(balanceDemoRecoveredAt<0f)balanceDemoRecoveredAt=t;
            }
        }

        private float BalanceCorrection()
        {
            float turnAcceleration=state.speed*state.speed/1.45f*Mathf.Tan(state.steeringAngle);
            return Mathf.Clamp(turnAcceleration/(1.05f*8f)-state.lean*1.55f-state.leanVelocity*.42f,-1f,1f);
        }

        private void SetQaDrive(float t,float targetSpeed,float steer,float balance)
        {
            input.throttle=Mathf.Clamp01(.22f+(targetSpeed-state.speed)*.22f);
            input.brake=Mathf.Clamp01((state.speed-targetSpeed-.35f)*.35f);
            input.steer=steer;input.balance=balance;
            input.clutch=state.gear==0?1:Mathf.Clamp01(1-(state.elapsed-.9f)*.9f);
            if(state.gear==0&&t-lastBotShift>.8f){input.shift=1;lastBotShift=t;}
            if(!state.engineRunning){input.clutch=1;input.ignition=true;}
        }

        private IEnumerator CaptureVideoFrames()
        {
            var target=new RenderTexture(videoWidth,videoHeight,24,RenderTextureFormat.ARGB32);
            var frame=new Texture2D(videoWidth,videoHeight,TextureFormat.RGB24,false);
            while(videoFramesWritten<videoFrameTarget)
            {
                yield return new WaitForEndOfFrame();
                var oldTarget=follow.targetTexture;var oldActive=RenderTexture.active;
                follow.targetTexture=target;follow.Render();RenderTexture.active=target;
                frame.ReadPixels(new Rect(0,0,videoWidth,videoHeight),0,0);frame.Apply(false);
                File.WriteAllBytes(Path.Combine(videoFramesFolder,$"frame-{videoFramesWritten:00000}.jpg"),frame.EncodeToJPG(88));
                follow.targetTexture=oldTarget;RenderTexture.active=oldActive;
                videoFramesWritten++;
            }
            target.Release();Destroy(target);Destroy(frame);Time.captureFramerate=0;
            Debug.Log("MOTOR_VIDEO_CAPTURE_PASS frames="+videoFramesWritten+" fps="+videoFps+" size="+videoWidth+"x"+videoHeight);
        }
        [Serializable] private class QaReport
        {
            public string mode,status;public int players,maxPlayers,localSlot,meshCount,antigravityAssets,mountainColliders,treeColliders,errors;
            public bool connected,playing,onRoad,balanceDemoFailed,balanceDemoRecovered;
            public float worldX,balanceDemoCrashAt,balanceDemoRestartAt,balanceDemoRecoveredAt;public BikeSnapshot state;
        }
        private IEnumerator SaveQa()
        {
            yield return new WaitForEndOfFrame();
            if(!string.IsNullOrEmpty(qaFolder))
            {
                Directory.CreateDirectory(qaFolder);
                var report=new QaReport{mode=qaMode,status=session.Status,players=session.PlayerCount,maxPlayers=maxPlayers,localSlot=session.LocalSlot,
                    meshCount=bike.MeshCount,antigravityAssets=world.AntigravityInstances,mountainColliders=world.MountainColliders,
                    treeColliders=world.TreeColliders,errors=qaErrors,connected=session.IsConnected,playing=Playing,
                    onRoad=MotorCourse.IsOnRoad(state.distance,state.lateral),worldX=MotorCourse.Sample(state.distance).centerX+state.lateral,
                    balanceDemoFailed=balanceDemoFailed,balanceDemoRecovered=balanceDemoRecovered,balanceDemoCrashAt=balanceDemoCrashAt,
                    balanceDemoRestartAt=balanceDemoRestartAt,balanceDemoRecoveredAt=balanceDemoRecoveredAt,state=state};
                File.WriteAllText(Path.Combine(qaFolder,"result.json"),JsonUtility.ToJson(report,true));
                if(qaCapture)
                {
                    // Hidden Windows test processes have no presented backbuffer. Render the actual game camera explicitly.
                    var target=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);
                    var oldTarget=follow.targetTexture;var oldActive=RenderTexture.active;
                    follow.targetTexture=target;follow.Render();RenderTexture.active=target;
                    var capture=new Texture2D(1440,900,TextureFormat.RGB24,false);
                    capture.ReadPixels(new Rect(0,0,1440,900),0,0);capture.Apply();
                    File.WriteAllBytes(Path.Combine(qaFolder,"scene.png"),capture.EncodeToPNG());Destroy(capture);
                    follow.targetTexture=oldTarget;RenderTexture.active=oldActive;target.Release();Destroy(target);
                    ScreenCapture.CaptureScreenshot(Path.Combine(qaFolder,"game.png"));
                }
                Debug.Log("MOTOR_RUNTIME_QA "+JsonUtility.ToJson(report));
            }
            yield return new WaitForSecondsRealtime(2);
            Application.Quit(qaErrors>0?2:0);
        }
        private void OnDestroy(){Time.captureFramerate=0;Application.logMessageReceived-=OnLog;}
    }
}
