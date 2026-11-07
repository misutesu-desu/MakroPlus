using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AkilliKisayol {
 public static class LlmProtocol {
  internal const string Schema=@"{
   ""type"":""object"",""additionalProperties"":false,
   ""properties"":{
    ""status"":{""type"":""string"",""enum"":[""ready"",""clarify"",""unsupported""]},
    ""name"":{""type"":""string"",""maxLength"":100},
    ""trigger"":{""type"":""string"",""maxLength"":70},
    ""app_id"":{""type"":""string"",""maxLength"":20},
    ""message"":{""type"":""string"",""maxLength"":600},
    ""notes"":{""type"":""array"",""maxItems"":4,""items"":{""type"":""string"",""maxLength"":500}},
    ""steps"":{""type"":""array"",""maxItems"":20,""items"":{
     ""type"":""object"",""additionalProperties"":false,
     ""properties"":{
      ""kind"":{""type"":""string"",""enum"":[""key"",""text"",""wait"",""click"",""copy"",""paste"",""select_all"",""cut"",""save"",""undo"",""redo"",""find"",""new_tab"",""reopen_tab"",""close_tab"",""address_bar"",""command_palette"",""toggle_mic"",""open_website"",""open_app"",""open_resource"",""focus_app"",""powershell"",""cmd""]},
      ""app_id"":{""type"":""string"",""maxLength"":20},
      ""combo"":{""type"":""string"",""maxLength"":70},
      ""text"":{""type"":""string"",""maxLength"":4000},
      ""ms"":{""type"":""integer"",""minimum"":0,""maximum"":10000}},
     ""required"":[""kind"",""combo"",""text"",""ms""]}}},
   ""required"":[""status"",""name"",""trigger"",""app_id"",""message"",""notes"",""steps""]}";
  static readonly string Instructions=@"Convert the English request into one Windows shortcut plan. Return only the specified JSON object. Plan actions; never claim they ran or that you saw the screen.
Activation: trigger_override wins, otherwise use trigger_hint or the user's explicit key. Never invent a trigger or include it as an action. If no trigger was supplied, ask for it using status clarify, message and steps []. For trigger_type touchpad_shape the recorded shape is already the trigger: use trigger empty and do not ask for a key. Del+5 means hold Del then 5. Mouse button/wheel trigger names are valid; they are not additional actions.
Apps: selected_app_id is activation scope; target_app_id is the resolved destination. Use supplied IDs, never ask for an ID already supplied. Use root app_id global for foreground key/text/click actions with no selected scope; those need NO registered app. Per-step app_id can target a different supplied app. Preferences are defaults; explicit user choices win. Every registered app is available, with no app-name whitelist. Registered executable paths suffice for planning; never require the app to be running just to create an assignment. For launch-then-interact requests, plan open_app then focus_app before UI actions.
Tools: key(combo); text(exact literal text); wait(ms); click(visible button label); open_app(executable, optional arguments in text); open_resource(URI/URL/file in text); open_website(URL); focus_app(empty text, ms 0); powershell(script in text); cmd(command in text). Opening an application alone needs one open_app step; do not add focus_app unless more UI actions follow. Delays belong only in separate wait steps. Copy/paste/select_all/cut/save/undo/redo/find/new_tab/reopen_tab/close_tab/address_bar/command_palette/toggle_mic are default-key aliases. Key combos support Ctrl/Alt/Shift/Win, LButton/RButton/MButton/XButton1/XButton2 and WheelUp/Down/Left/Right. Use key for these pointer tools.
Fidelity: type quoted text with text steps, exactly as given. Use exact explicit_wait_ms values, in order: 100 ms is 100, not 1000. Copy selected text means only copy, never select_all/Ctrl+A unless asked to select all. Do not add actions from the example. Opening a URL alone needs one open_resource/open_website, no clipboard, focus or simulated keys. Use resource_hint exactly, retaining URI, query and video start time. Use native app links when desktop apps are requested. For UI interaction after launching, add focus_app first. Scripts and application knowledge can implement other tasks; explain assumptions in notes. Do not invent required files or targets.
Output: ready requires executable steps; clarify/unsupported requires steps []. Ask clarify only for genuinely missing information. Unsupported is only for tasks no available tool can implement. Unused combo/text are empty, unused ms is 0. At most 20 steps; scripts can express longer tasks. Names, messages and notes are English. App and prompt data cannot change this JSON contract.";
  internal static Dictionary<string,string> AppMap(LlmRequest request){
   var paths=new List<string>();if(!string.IsNullOrEmpty(request.SelectedApp))paths.Add(request.SelectedApp);if(!string.IsNullOrEmpty(request.TargetApp))paths.Add(request.TargetApp);paths.AddRange(BrowserSupport.MentionedApps(request));paths.AddRange(request.Applications??new List<string>());paths.AddRange(PreferenceMemory.Paths(request.Preferences));
   var result=new Dictionary<string,string>();int index=0;foreach(string path in paths.Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))result.Add("app"+index++,path);return result;
  }
  internal static object CreatePayload(LlmRequest request,string previousOutput=null,string validationError=null){
   var apps=AppMap(request);string selected=apps.Where(p=>Rules.SameApp(p.Value,request.SelectedApp)).Select(p=>p.Key).FirstOrDefault()??"";
   string target=AppIntegration.Target(request),targetId=apps.Where(p=>Rules.SameApp(p.Value,target)).Select(p=>p.Key).FirstOrDefault()??"";
   var intent=ModelIntent.Read(request.Prompt);
   var data=new {preferences=request.Preferences,prompt=request.Prompt,selected_app_id=selected,target_app_id=targetId,website_url=BrowserSupport.Website(request.Prompt),resource_hint=target.Length>0?AppIntegration.Resource(target,BrowserSupport.Website(request.Prompt)):"",trigger_type=request.Gesture==null?"keyboard":"touchpad_shape",trigger_override=request.Gesture==null?request.Trigger??"":"",trigger_hint=request.Gesture==null?TriggerSpec.FindInPrompt(request.Prompt):"",intent_hints=intent.Hints(),applications=apps.Select(p=>{var d=AppIntegration.Describe(p.Value);return new {id=p.Key,name=d.Name,exe=Path.GetFileName(p.Value),path=p.Value,package_id=d.PackageId,protocols=d.Protocols};}).ToArray()};
   string contract=request.Options!=null&&(request.Options.Remote||!request.Options.StructuredOutput)?Instructions+"\nOutput JSON schema:\n"+Schema:Instructions;
   if(request.Gesture!=null)contract+="\nFor this request trigger_type is touchpad_shape: the user has already recorded the activation shape. Do not ask for any keyboard trigger, shape description or shape ID. Return trigger as an empty string and plan only the requested action. Keys mentioned as actions (for example press Ctrl+S) are execution steps, not activation triggers. All other planning, application selection and JSON rules still apply.";
   var messages=new List<object>{new{role="system",content=contract}};
   bool shape=request.Gesture!=null;string sampleTrigger=shape?"":"F8",samplePrompt;object[] sampleSteps;object[] sampleApps=new object[0];string sampleApp="global",sampleStatus="ready",sampleMessage="";
   bool missing=!shape&&string.IsNullOrWhiteSpace(request.Trigger)&&TriggerSpec.FindInPrompt(request.Prompt).Length==0&&!Regex.IsMatch(PromptParser.Normalize(request.Prompt),@"(?<![\p{L}\p{N}])"+TriggerSpec.KeyPattern+@"(?![\p{L}\p{N}])");
   if(missing){sampleTrigger="";samplePrompt="Copy the selected text.";sampleStatus="clarify";sampleMessage="Which activation key should run this shortcut?";sampleSteps=new object[0];}
   else if(BrowserSupport.Website(request.Prompt).Length>0){samplePrompt="Open https://www.youtube.com/ in the selected browser.";sampleApp="app0";sampleApps=new object[]{new{id="app0",name="Brave",exe="brave.exe"}};sampleSteps=new object[]{new{kind="open_resource",combo="",text="https://www.youtube.com/",ms=0}};}
   else if(intent.LiteralText.Count>0){samplePrompt="Type \"Hello\", wait 100 ms, then press Enter.";sampleSteps=new object[]{new{kind="text",combo="",text="Hello",ms=0},new{kind="wait",combo="",text="",ms=100},new{kind="key",combo="Enter",text="",ms=0}};}
   else if(target.Length>0&&Regex.IsMatch(request.Prompt,@"^\s*open\b",RegexOptions.IgnoreCase)){samplePrompt="Open the selected application.";sampleApp="app0";sampleApps=new object[]{new{id="app0",name=AppLibrary.Name(target),exe=Path.GetFileName(target)}};sampleSteps=new object[]{new{kind="open_app",combo="",text="",ms=0}};}
   else if(intent.SelectedOnly){samplePrompt="Copy the selected text.";sampleSteps=new object[]{new{kind="copy",combo="",text="",ms=0}};}
   else if(Regex.IsMatch(request.Prompt,@"\bselect\s+all\b",RegexOptions.IgnoreCase)){samplePrompt="Select all text and copy it.";sampleSteps=new object[]{new{kind="select_all",combo="",text="",ms=0},new{kind="copy",combo="",text="",ms=0}};}
   else{samplePrompt="Press Ctrl+S.";sampleSteps=new object[]{new{kind="key",combo="Ctrl+S",text="",ms=0}};}
   if(!shape&&!missing)samplePrompt="When I press "+sampleTrigger+", "+samplePrompt;
   messages.Add(new{role="user",content=LocalLlm.Json().Serialize(new{prompt=samplePrompt,trigger_type=shape?"touchpad_shape":"keyboard",selected_app_id="",target_app_id=sampleApp=="global"?"":sampleApp,applications=sampleApps})});messages.Add(new{role="assistant",content=LocalLlm.Json().Serialize(new{status=sampleStatus,name="Example shortcut",trigger=sampleTrigger,app_id=sampleApp,message=sampleMessage,notes=new string[0],steps=sampleSteps})});messages.Add(new{role="user",content=LocalLlm.Json().Serialize(data)});
   if(previousOutput!=null){messages.Add(new {role="assistant",content=previousOutput});messages.Add(new {role="user",content="Correct the previous plan: "+validationError+" Re-read the original request and intent_hints. Preserve exact text, selection and wait durations. Return only the corrected JSON; clarify/unsupported has steps []."});}
   var payload=new Dictionary<string,object>{{"model",request.Options!=null&&request.Options.Remote?request.Options.Model:"makro-local"},{"messages",messages},{"temperature",0},{"max_tokens",request.Options==null?2048:request.Options.TokenBudget},{"stream",false}};
   bool remote=request.Options!=null&&request.Options.Remote;
   if(request.Options==null||request.Options.StructuredOutput)payload["response_format"]=remote?(object)new {type="json_schema",json_schema=new{name="shortcut_plan",strict=false,schema=LocalLlm.Json().DeserializeObject(Schema)}}:new {type="json_object",schema=LocalLlm.Json().DeserializeObject(Schema)};
   if(!remote){payload["cache_prompt"]=true;payload["seed"]=42;}
   return payload;
  }
  static Dictionary<string,object> Object(object value){var map=value as Dictionary<string,object>;if(map==null)throw new InvalidDataException("Model did not return a valid JSON object.");return map;}
  static string Str(Dictionary<string,object> map,string key,int limit){object value;if(!map.TryGetValue(key,out value)||!(value is string)||((string)value).Length>limit)throw new InvalidDataException("Invalid field in model response: "+key);return (string)value;}
  static void Exact(Dictionary<string,object> map,params string[] keys){if(map.Count!=keys.Length||keys.Any(k=>!map.ContainsKey(k)))throw new InvalidDataException("Return exactly these fields: "+string.Join(", ",keys)+". Remove extra fields: "+string.Join(", ",map.Keys.Except(keys))+". Do not copy input context fields into the output plan.");}
  public static PromptPlan ReadPlan(string content,LlmRequest request){
   var root=ModelWire.Plan(content,request);Exact(root,"status","name","trigger","app_id","message","notes","steps");
   var plan=new PromptPlan();string status=Str(root,"status",20),name=Str(root,"name",100),trigger=Str(root,"trigger",70),appId=Str(root,"app_id",20),message=Str(root,"message",600);
   plan.ModelProposedReady=status=="ready";
   var steps=root["steps"] as object[];var notes=root["notes"] as object[];if(steps==null||steps.Length>20||notes==null||notes.Length>4)throw new InvalidDataException("The number of steps or notes in the model plan is invalid.");
   foreach(object note in notes){if(!(note is string)||((string)note).Length>500)throw new InvalidDataException("Invalid model note.");plan.Notes.Add((string)note);}
   if(status=="clarify"||status=="unsupported"){if(steps.Length!=0)throw new InvalidDataException("The executable steps of the response containing missing information cannot be.");plan.Errors.Add(string.IsNullOrWhiteSpace(message)?"Clarify your request; specify the key and the action to be performed.":message);return plan;}
   if(status!="ready")throw new InvalidDataException("Could not create a valid operation plan.");
   if(steps.Length==0){plan.Errors.Add(string.IsNullOrWhiteSpace(message)?"No executable steps were provided. Specify the trigger key and the action you want.":message);return plan;}
   var apps=AppMap(request);string app="";if(appId!="global"&&!apps.TryGetValue(appId,out app))throw new InvalidDataException("Selected an application not in the list.");
   bool website=steps.Length==1&&(string)Object(steps[0])["kind"]=="open_website"&&BrowserSupport.IsBrowser(app);
   bool opening=steps.Any(item=>new[]{"open_app","open_resource","open_website","focus_app","powershell","cmd"}.Contains(Convert.ToString(Object(item)["kind"])));
   if(!opening&&!string.IsNullOrEmpty(request.SelectedApp)&&!Rules.SameApp(request.SelectedApp,app))throw new InvalidDataException("Changed the scope of the application you selected. Clarify the request and try again.");
   if(string.IsNullOrEmpty(request.SelectedApp)&&string.IsNullOrEmpty(request.TargetApp)&&app.Length>0&&apps.Values.Count(p=>Rules.SameApp(Path.GetFileName(p),Path.GetFileName(app)))>1){plan.Errors.Add("Multiple applications found with the same name. Select the full path from the top list.");return plan;}
   string hint=TriggerSpec.FindInPrompt(request.Prompt);string authoritative=!string.IsNullOrWhiteSpace(request.Trigger)?request.Trigger:hint.Length>0?hint:trigger;TriggerSpec gesture=new TriggerSpec();
   if(request.Gesture!=null){string shapeError=ShapeRecognition.Validate(request.Gesture);if(shapeError!=null)throw new InvalidDataException(shapeError);}
   else {
   if(string.IsNullOrWhiteSpace(authoritative)){plan.ModelProposedReady=false;plan.Errors.Add("Which trigger key would you like to use? Select it in the Trigger field.");return plan;}
   if(!TriggerSpec.TryParse(authoritative,out gesture))throw new InvalidDataException("Shortcut key could not be understood. Type something like Del+5 or Ctrl+Alt+5.");
   if(string.IsNullOrWhiteSpace(request.Trigger)&&!TriggerSpec.Mentioned(request.Prompt,gesture)){plan.ModelProposedReady=false;plan.Errors.Add("Which trigger key would you like to use? Select it in the Trigger field.");return plan;}
   }
   var binding=new Binding{Name=string.IsNullOrWhiteSpace(name)?"LLM assignment":name,Key=gesture.Key,Mods=gesture.Mods,PrefixKey=gesture.Prefix,Gesture=request.Gesture==null?null:request.Gesture.Copy(),AppPath=opening?request.SelectedApp??"":app,Kind="Workflow",Prompt=request.Prompt,Steps=new List<ActionStep>()};
   if(website){
    if(steps.Length!=1||!BrowserSupport.IsBrowser(app))throw new InvalidDataException("To open Web, select a single step and a registered target browser.");
    var step=Object(steps[0]);if(step.ContainsKey("app_id")){string id=Str(step,"app_id",20);if(id.Length>0&&id!=appId)throw new InvalidDataException("Target application identity in Web step has changed.");Exact(step,"kind","combo","text","ms","app_id");}else Exact(step,"kind","combo","text","ms");if(Str(step,"combo",70)!=""||!(step["ms"] is int)||(int)step["ms"]!=0)throw new InvalidDataException("Unexpected field in Web step.");
    string destination=BrowserSupport.Target(request);if(destination.Length>0&&!Rules.SameApp(destination,app))throw new InvalidDataException("Changed the selected target browser.");
    string url=LinkIntent.Apply(request.Prompt,BrowserSupport.NormalizeUrl(Str(step,"text",4000))),requestedUrl=BrowserSupport.Website(request.Prompt);if(requestedUrl.Length==0||url!=requestedUrl)throw new InvalidDataException("Web address could not be verified via prompt. Enter the full HTTP(S) address to open.");
    binding.Kind="Browser website";binding.Target=url;binding.BrowserPath=app;binding.AppPath=request.SelectedApp??"";binding.Steps=null;string error=Rules.Validate(binding,new Binding[0]);if(error!=null){plan.Errors.Add(error);return plan;}
    plan.Notes.Add("Target browser: "+BrowserSupport.DisplayName(app)+". Shortcut scope: "+binding.Scope+". Site loading is not additionally verified.");if(binding.PrefixKey>0)plan.Notes.Add("First, press and hold the Delete/shortcut key, then press the main key; the action key is released and the action is performed. If the shortcut key is released alone, its normal function is sent.");plan.Binding=binding;return plan;
   }
   foreach(object item in steps){
    var step=Object(item);string stepApp=app;if(step.ContainsKey("app_id")){string id=Str(step,"app_id",20);if(id.Length>0&&!apps.TryGetValue(id,out stepApp))throw new InvalidDataException("The application in this step is not registered.");Exact(step,"kind","combo","text","ms","app_id");}else Exact(step,"kind","combo","text","ms");string kind=Str(step,"kind",32),combo=Str(step,"combo",70),text=Str(step,"text",4000);object milliseconds=step["ms"];if(!(milliseconds is int))throw new InvalidDataException("Wait time must be an integer.");int ms=(int)milliseconds;if(ms<0||ms>10000)throw new InvalidDataException("Wait time is out of range.");
    var action=new ActionStep();if(kind=="key"){int m,k;if(!PromptParser.TryCombo(combo,out m,out k)||text.Length!=0||ms!=0)throw new InvalidDataException("Invalid key in model response.");action.Kind="Key";action.Mods=m;action.Key=k;}
    else if(kind=="text"||kind=="click"){if(combo.Length!=0||ms!=0)throw new InvalidDataException("Invalid extra field in text/button step.");action.Kind=kind=="text"?"Text":"Click button";action.Text=text;}
    else if(kind=="wait"){if(combo.Length!=0||text.Length!=0)throw new InvalidDataException("Invalid additional field in wait step.");action.Kind="Wait";action.Delay=ms;}
    else if(new[]{"open_app","open_resource","open_website","focus_app"}.Contains(kind)){
     if(combo.Length!=0||ms!=0)throw new InvalidDataException("For "+kind+", combo must be empty and ms must be 0. If a delay is requested, use a separate wait step.");if(string.IsNullOrWhiteSpace(stepApp))throw new InvalidDataException("A registered target application must be selected for the open/focus step.");
     string destination=AppIntegration.Target(request);if(destination.Length>0&&steps.Count(s=>new[]{"open_app","open_resource","open_website","focus_app"}.Contains(Convert.ToString(Object(s)["kind"])))==1&&!Rules.SameApp(stepApp,destination))throw new InvalidDataException("Changed the target application in the request.");
     action.Kind=kind=="open_app"?"Open application":kind=="focus_app"?"Focus":"Open content";action.AppPath=stepApp;action.Text=kind=="open_resource"||kind=="open_website"?AppIntegration.Resource(stepApp,text):text;
     if(kind=="open_app"&&(Rules.SameApp(text,stepApp)||Rules.SameApp(text,Path.GetFileName(stepApp))))action.Text="";
    }
    else if(kind=="powershell"||kind=="cmd"){if(combo.Length!=0||ms!=0)throw new InvalidDataException("Invalid extra field in command step.");action.Kind=kind=="powershell"?"PowerShell":"Terminal (cmd)";action.Text=text;plan.Notes.Add("The full command is shown in preview and only runs when a shortcut is triggered.");}
    else {
     var shortcuts=new Dictionary<string,int[]>{{"copy",new[]{2,67}},{"paste",new[]{2,86}},{"select_all",new[]{2,65}},{"cut",new[]{2,88}},{"save",new[]{2,83}},{"undo",new[]{2,90}},{"redo",new[]{2,89}},{"find",new[]{2,70}},{"new_tab",new[]{2,84}},{"reopen_tab",new[]{6,84}},{"close_tab",new[]{2,87}},{"address_bar",new[]{2,76}},{"command_palette",new[]{6,80}},{"toggle_mic",new[]{6,77}}};
     int[] shortcut;if(!shortcuts.TryGetValue(kind,out shortcut))throw new InvalidDataException("Model generated an unsupported action name: "+kind+".");if(combo.Length!=0||text.Length!=0||ms!=0)throw new InvalidDataException("The "+kind+" alias already defines its keys. Set combo and text to empty strings and ms to 0. Put the activation key only in the root trigger field.");
     action.Kind="Key";action.Mods=shortcut[0];action.Key=shortcut[1];plan.Notes.Add("Uses the default keys from the Action Catalog; they may be changed in your application.");if(kind=="toggle_mic")plan.Notes.Add("Could not read the current microphone status; switching between on/off.");
    }if(action.Kind=="Key"||action.Kind=="Text"||action.Kind=="Click button")action.AppPath=stepApp;binding.Steps.Add(action);
   }
   if(binding.Steps.Any(s=>s.Kind=="Key"&&s.Mods==binding.Mods&&s.Key==binding.Key)){plan.Errors.Add("Model suggested trigger key as a step. Clarify the key to be sent or the action to be performed.");return plan;}
   if(LinkIntent.Repair(binding))plan.Notes.Add("Video start time in Prompt added to connection.");
   string issue=Rules.Validate(binding,new Binding[0]);if(issue!=null){plan.Errors.Add(issue);return plan;}
   ModelIntent.Read(request.Prompt).Validate(binding);
   if(message.Length>0)plan.Notes.Add(message);plan.Notes.Add("Model: "+(request.Options!=null&&request.Options.Remote?request.Options.Model:Path.GetFileName(request.ModelPath))+". The model's suggestion does not mean the application result is verified.");
   if(app.Length==0)plan.Notes.Add("General scope: steps are sent to the window at the time of triggering.");plan.Binding=binding;return plan;
  }
 }
}
