using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MonsterKitchen.Editor
{
    /// <summary>Editor-only integration checks. Uses real keyboard events and live game components.</summary>
    public static class KitchenIntegrationChecks
    {
        public static string LastResult = "Not run";
        static int checks;
        static Key[] held = Array.Empty<Key>();
        static Keyboard testKeyboard, originalKeyboard;
        static InputSettings originalSettings, testSettings;
        static void Require(bool value,string message) { if(!value) throw new Exception("CHECK FAILED: "+message); checks++; }
        static void FeedKeyboard()
        {
            if (InputState.currentUpdateType == InputUpdateType.Dynamic && testKeyboard != null)
                InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(held));
        }
        static void BeginInput()
        {
            originalKeyboard=Keyboard.current;originalSettings=InputSystem.settings;
            testSettings=UnityEngine.Object.Instantiate(originalSettings);
            testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings=testSettings;
            testKeyboard=InputSystem.AddDevice<Keyboard>();testKeyboard.MakeCurrent();
            InputSystem.onBeforeUpdate+=FeedKeyboard;
        }
        static void EndInput()
        {
            held=Array.Empty<Key>();InputSystem.onBeforeUpdate-=FeedKeyboard;
            if(testKeyboard!=null)InputSystem.RemoveDevice(testKeyboard);
            testKeyboard=null;
            if(originalKeyboard!=null&&originalKeyboard.added)originalKeyboard.MakeCurrent();
            InputSystem.settings=originalSettings;
            if(testSettings)UnityEngine.Object.Destroy(testSettings);
        }
        static async Task Press(Key key,int milliseconds=140)
        {
            held=new[]{key}; await Task.Delay(milliseconds); held=Array.Empty<Key>(); await Task.Delay(100);
        }
        static KitchenStation Find(StationType type,int slot=0) => KitchenStation.All.First(s=>s.Type==type && (type!=StationType.Serve||s.ServiceSlot==slot));
        static async Task Approach(KitchenStation station)
        {
            if(station.Type==StationType.Serve)
                await WaitForGuests();
            PlayerChef.Instance.Teleport(station.ReachPoint.position+Vector3.up*.08f);
            await Task.Delay(180);
            Require(station.PlayerInside,"station collision trigger detects chef: "+station.name);
            Require(PlayerChef.Instance.Target==station,"correct station target: "+station.name);
        }
        static async Task WaitForGuests()
        {
            int guard=0;
            while(GameSession.Instance.Orders.Any(t=>!t.AtCounter)&&guard++<150) await Task.Delay(100);
            Require(GameSession.Instance.Orders.All(t=>t.AtCounter),"guests reach the serving counter through the doorway");
        }
        public static async Task<string> Run()
        {
            if(!Application.isPlaying) throw new Exception("Enter Play mode in Kitchen first.");
            checks=0;
            LastResult="Running";
            BeginInput();
            try
            {
                var session=GameSession.Instance; var chef=PlayerChef.Instance;
                session.RestartShift(); await Task.Delay(160);
                Vector3 start=chef.transform.position;
                await Press(Key.D,350);
                Require(Vector3.Distance(start,chef.transform.position)>.7f,"continuous keyboard movement");
                var supply=KitchenStation.All.First(s=>s.Type==StationType.Supply&&s.SupplyFood==FoodKind.Patty);
                var stove=Find(StationType.Hotplate);
                await Approach(supply); await Press(Key.E);
                Require(chef.Held && chef.Held.Kind==FoodKind.Patty,"keyboard ingredient pickup");
                var portion=chef.Held;
                await Press(Key.E);
                Require(chef.Held==portion,"full hands do not duplicate ingredients");
                await Approach(stove); await Press(Key.E);
                Require(stove.Item==portion && !chef.Held,"atomic hands-to-hotplate transfer");
                float beforeHeat=portion.HeatSeconds; await Task.Delay(450);
                Require(portion.HeatSeconds>beforeHeat+.2f,"hotplate advances naturally");
                await Press(Key.Escape);
                Require(session.State==RunState.Paused,"keyboard pause");
                float frozenHeat=portion.HeatSeconds, frozenPatience=session.Orders[0].Remaining;
                await Task.Delay(350);
                Require(Mathf.Approximately(frozenHeat,portion.HeatSeconds)&&Mathf.Approximately(frozenPatience,session.Orders[0].Remaining),"pause freezes cooking and patience");
                await Press(Key.Escape); Require(session.IsRunning,"keyboard resume");
                portion.AddHeat(7.1f-portion.HeatSeconds);
                await Press(Key.E);
                Require(chef.Held==portion && portion.Heat==HeatState.Cooked && !stove.Item,"collect cooked food");
                frozenHeat=portion.HeatSeconds; await Task.Delay(250);
                Require(portion.HeatSeconds==frozenHeat,"carrying stops heat");
                await Approach(Find(StationType.Serve,session.Orders[0].Slot)); await Press(Key.E);
                Require(session.Served==1&&!chef.Held,"successful real service consumes one dish");
                await Task.Delay(2200);
                Require(session.Orders.Count==1&&session.Orders[0].Required==HeatState.Charred,"second teaching order requests charred food");
                await Approach(supply); await Press(Key.E);
                var wrong=chef.Held;
                await Approach(Find(StationType.Serve,session.Orders[0].Slot)); await Press(Key.E);
                Require(chef.Held==wrong && session.Served==1,"wrong delivery preserves food and ticket");
                wrong.AddHeat(12.2f);
                await Press(Key.E); Require(session.Served==2,"charred order accepted");
                await Task.Delay(2200);
                Require(session.Orders.Count==2,"first mixed pair overlaps");
                var cookedTicket=session.Orders.First(t=>t.Required==HeatState.Cooked);
                var charredTicket=session.Orders.First(t=>t.Required==HeatState.Charred);
                await Approach(supply); await Press(Key.E); chef.Held.AddHeat(12.1f);
                await Approach(Find(StationType.Serve,charredTicket.Slot)); await Press(Key.E);
                Require(session.Orders.Contains(cookedTicket)&&session.Served==3,"salvage leaves original cooked order pending");
                await Approach(supply); await Press(Key.E); chef.Held.AddHeat(30);
                await Approach(Find(StationType.Serve,cookedTicket.Slot)); await Press(Key.E);
                Require(chef.Held && chef.Held.Heat==HeatState.Ash,"ash is rejected");
                await Approach(Find(StationType.Trash)); await Press(Key.E);
                Require(!chef.Held&&session.Wasted==1,"discard frees hands and records waste");
                // Boundary checks exercise CharacterController collision against authored walls.
                chef.Teleport(new Vector3(6.5f,.08f,-3.3f));
                for(int i=0;i<50;i++) chef.Move(new Vector2(1,0),.03f);
                Require(chef.transform.position.x<7.2f,"east boundary blocks movement");
                session.RestartShift(); await Task.Delay(100);
                Require(session.Served==0&&session.Complaints==0&&session.Wasted==0&&!chef.Held&&KitchenStation.All.All(s=>!s.Item),"restart resets runtime state");
                // Resolve remaining order schedules quickly, still through the actual acceptance API.
                int guard=0;
                while(session.IsRunning && guard++<30)
                {
                    await WaitForGuests();
                    foreach(var ticket in session.Orders.ToArray())
                    {
                        string reason; session.TryServe(ticket.Slot,ticket.Food,ticket.Required,out reason);
                        if(!session.IsRunning) break;
                    }
                    await Task.Delay(2200);
                }
                Require(session.State==RunState.Won&&session.Served==8,"complete authored shift reaches win");
                session.RestartShift(); guard=0;
                while(session.IsRunning&&guard++<8)
                {
                    await WaitForGuests();
                    foreach(var ticket in session.Orders) ticket.Remaining=.01f;
                    await Task.Delay(2200);
                }
                Require(session.State==RunState.Lost&&session.Complaints==3&&session.Served==0,"waiting reaches loss, never win");
                session.RestartShift(); session.TogglePause();
                LastResult=checks+" Unity integration checks passed (keyboard, collisions, ownership, cooking, recovery, pause, restart, win and loss).";
                Debug.Log(LastResult);
                return LastResult;
            }
            catch(Exception e) { LastResult=e.Message; Debug.LogError(e); throw; }
            finally { EndInput(); }
        }

        /// <summary>Exercises the paper menus and leaves a real completed shift visible for review.</summary>
        public static async Task<string> PaperUI()
        {
            checks=0;LastResult="Checking paper UI";BeginInput();
            bool Has(string text)=>UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>().Any(t=>t.text==text&&t.gameObject.activeInHierarchy);
            try
            {
                var session=GameSession.Instance;session.RestartShift();await Task.Delay(180);
                await Press(Key.Escape);Require(Has("Paused"),"pause sheet appears");
                await Press(Key.S);await Press(Key.Enter);Require(Has("How to play")&&Has("Take, place or serve"),"pause opens controls");
                await Press(Key.Escape);Require(Has("Paused"),"controls return to paused shift");
                await Press(Key.S);await Press(Key.S);await Press(Key.Enter);Require(Has("Sound"),"pause opens sound page");
                var slider=UnityEngine.Object.FindAnyObjectByType<UnityEngine.UI.Slider>();
                float oldVolume=slider.value;
                slider.value=oldVolume>.5f?.25f:.75f;await Task.Delay(100);
                Require(Mathf.Approximately(AudioListener.volume,slider.value),"volume slider changes playback");
                slider.value=oldVolume;
                await Press(Key.M);Require(AudioListener.volume==0,"mute key silences audio");
                await Press(Key.M);Require(Mathf.Approximately(AudioListener.volume,oldVolume),"unmute restores volume");
                await Press(Key.Escape);Require(Has("Paused"),"sound returns to pause");
                await Press(Key.S);await Press(Key.S);await Press(Key.S);await Press(Key.Enter);
                Require(session.IsRunning&&session.Served==0&&session.Complaints==0,"restart choice begins a clean shift");
                var pauseButton=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().First(b=>b.name=="Pause");
                pauseButton.onClick.Invoke();await Task.Delay(100);Require(session.State==RunState.Paused,"HUD pause button is wired");
                await Press(Key.Enter);Require(session.IsRunning,"resume choice returns to game");
                int guard=0;
                while(session.IsRunning&&guard++<30)
                {
                    await WaitForGuests();
                    foreach(var ticket in session.Orders.ToArray())
                    {
                        string reason;session.TryServe(ticket.Slot,ticket.Food,ticket.Required,out reason);
                        if(!session.IsRunning)break;
                    }
                    await Task.Delay(2200);
                }
                Require(session.State==RunState.Won&&Has("Shift complete"),"successful shift displays receipt");
                Require(Has(session.Score.ToString())&&Has("8 / 8"),"receipt shows actual score and served count");
                Require(UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>().All(t=>t.font&&t.font.atlasTexture),"visible text uses bundled font and atlas");
                LastResult=checks+" paper UI checks passed (controls, sound, restart, pause, resume and completed-shift receipt).";
                Debug.Log(LastResult);return LastResult;
            }
            catch(Exception e){LastResult=e.Message;Debug.LogError(e);throw;}
            finally{EndInput();}
        }

        public static async Task<string> Menu()
        {
            checks=0;LastResult="Running cookbook menu checks";BeginInput();
            try
            {
                Require(UnityEngine.Object.FindAnyObjectByType<CookbookMenu>()!=null,"cookbook menu is present");
                await Task.Delay(200);await Press(Key.S);await Press(Key.Enter);
                Require(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>().Any(t=>t.text=="The house rules"),"keyboard opens book instructions");
                await Press(Key.Escape);await Press(Key.Enter);await Task.Delay(600);
                Require(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Kitchen","book Start loads Kitchen");
                Require(GameSession.Instance.State==RunState.Briefing,"arrives at briefing before time starts");
                await Press(Key.Enter);Require(GameSession.Instance.IsRunning,"keyboard starts gameplay from briefing");
                await Press(Key.Escape);Require(GameSession.Instance.State==RunState.Paused,"keyboard pauses after menu transition");
                LastResult=checks+" cookbook navigation checks passed.";return LastResult;
            }
            catch(Exception e){LastResult=e.Message;Debug.LogError(e);throw;}
            finally{EndInput();}
        }

        public static async Task<string> CustomerRoutes()
        {
            checks=0;LastResult="Checking customer routes and animation";
            try
            {
                var session=GameSession.Instance;session.RestartShift();await Task.Delay(120);
                var ticket=session.Orders[0];
                var guest=UnityEngine.Object.FindObjectsByType<GuestMotion>().First(g=>g.TicketId==ticket.Id);
                Require(!ticket.AtCounter,"walking guest cannot be served yet");
                float patience=ticket.Remaining;
                string reason;
                Require(!session.TryServe(ticket.Slot,ticket.Food,ticket.Required,out reason),"serving waits for physical customer arrival");
                await Task.Delay(300);
                Require(ticket.Remaining==patience,"doorway walk does not spend patience");
                Require(guest.IsWalking&&guest.Animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"),"walk animation plays while entering");
                Vector3 pausedPosition=guest.transform.position;
                session.TogglePause();await Task.Delay(250);
                Require(Vector3.Distance(pausedPosition,guest.transform.position)<.001f,"pause freezes customer movement");
                session.TogglePause();await WaitForGuests();await Task.Delay(250);
                Require(guest.Animator.GetCurrentAnimatorStateInfo(0).IsName("Wave"),"customer greets the chef on arrival");
                Require(Vector3.Dot(guest.transform.forward,Vector3.back)>.99f,"waiting guest faces the serving counter");
                Require(session.TryServe(ticket.Slot,ticket.Food,ticket.Required,out reason),"arrived customer accepts the requested meal");
                await Task.Delay(250);
                Require(guest.Animator.GetCurrentAnimatorStateInfo(0).IsName("Cheer"),"served customer celebrates before leaving");
                bool usedDoor=false, safeRoute=true, walkedOut=false;
                int guard=0;
                while(guest&&guard++<140)
                {
                    Vector3 p=guest.transform.position;
                    safeRoute &= p.z>=3.5f && (p.x<6.9f || (p.z>4.4f&&p.z<7.1f));
                    usedDoor |= p.x>7.3f;
                    walkedOut |= guest.IsWalking;
                    await Task.Delay(100);
                }
                Require(safeRoute,"exit stays in the customer aisle and doorway, clear of kitchen appliances");
                Require(usedDoor&&walkedOut&&!guest,"customer walks through exit and despawns outside");
                session.RestartShift();session.TogglePause();
                LastResult=checks+" customer route and animation checks passed.";Debug.Log(LastResult);return LastResult;
            }
            catch(Exception e){LastResult=e.Message;Debug.LogError(e);throw;}
        }
    }
}
