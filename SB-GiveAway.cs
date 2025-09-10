using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class CPHInline
{
    private GiveawayBot gway;

    public void Init()
    {
        gway = new GiveawayBot(sendMessage, sendDiscordMessage, obsSetGdiText, obsSetFilterState, playSoundFromFolder, obsSetSourceVisibility);
    }

    public bool Execute()
    {
        // your main code goes here
        return true;
    }

    public bool StartGiveaway()
    {
        StreamerBotHelper.Init(CPH);
    
        // get durations for the giveaway
        if (!(CPH.TryGetArg("giveawayDuration", out object duration)
            && CPH.TryGetArg("giveawayReminderDuration", out object reminderTiming)
            && CPH.TryGetArg("giveawayClaimTime", out object timeToClaim)))
        {
            CPH.SendMessage("Unable to retreive some of the Duration values");
            return false;
        }

        List<string> stringParametersToGet = new List<string>(new string[] { "broadcastUser", "rawInput", "giveawayStartMessage", "giveawayReminderMessage", "giveawayClosedMessage", "giveawayWinnerMessage", "giveawayClaimMessage", "giveawayRedrawMessage", "giveawayDiscordWebhookLink", "giveawayStartSound", "giveawayPrepButtsSound", "giveawayWinnerSound", "giveawayRedrawSound"});
        Dictionary<string, string> messages = new Dictionary<string, string>();
        List<string> unableToFind = new List<string>();

        for (int i = 0; i < stringParametersToGet.Count; i++)
        {
            if (CPH.TryGetArg(stringParametersToGet[i], out string outstring))
            {
                messages.Add(stringParametersToGet[i], outstring);
            }
            else
            {
                unableToFind.Add(stringParametersToGet[i]);
            }
        }

        if(unableToFind.Count > 0)
        {
            string output = "Unable to find following parameters: ";
            unableToFind.ForEach(item => output += item + ", ");
            output = output.Substring(0, output.Length - 2);
            CPH.SendMessage(output);
            return false;
        }

        TwitchUserInfoEx broadcaster = CPH.TwitchGetExtendedUserInfoByLogin(messages["broadcastUser"]);
        messages.Add("gameId", broadcaster.GameId);

        bool needsApi = false;
        CPH.TryGetArg<bool>("needsApiCheck", out needsApi);

        // start giveaway
        gway.startGiveaway(int.Parse(duration + ""), int.Parse(reminderTiming + ""), int.Parse(timeToClaim + ""), messages, needsApi);

        return true;
    }

    public bool StopGiveaway()
    {
        StreamerBotHelper.Init(CPH);

        gway.end();
        return true;
    }

    public bool enter()
    {
        StreamerBotHelper.Init(CPH);

        string user = "";
        if (!CPH.TryGetArg<string>("user", out user))
        {
            return false;
        }
        return gway.enter(user);
    }

    public bool claim()
    {
        StreamerBotHelper.Init(CPH);

        string user = "";

        if (!CPH.TryGetArg<string>("user", out user))
        {
            return false;
        }

        return gway.claim(user);
    }

    // callbacks for the GiveawayBot
    public void sendMessage(string type, string message, bool bot = true, params string[] args)
    {
        // iterate over args in case we need to replace some placeholders in the message
        for (int i = 1; i <= args.Length; i++)
        {
            message = message.Replace("$" + i, args[i - 1]);
        }

        // send the message to twitch chat
        switch (type)
        {
            case "announce":
                CPH.TwitchAnnounce(message, bot);
                break;
            case "message":
                CPH.SendMessage(message, bot);
                break;
            default:
                CPH.LogInfo("Sent message as Message but that might not be right!");
                CPH.SendMessage(message, bot);
                break;
        }
    }

    public void sendDiscordMessage(string webhookUrl, string content) => CPH.DiscordPostTextToWebhook(webhookUrl, content);
    public void obsSetGdiText(string scene, string source, string text) => CPH.ObsSetGdiText(scene, source, text);
    public void obsSetFilterState(string scene, string filterName, int state) => CPH.ObsSetFilterState(scene, filterName, state);
    public void obsSetSourceVisibility(string scene, string source, bool visible) => CPH.ObsSetSourceVisibility(scene, source, visible);
    public Double playSoundFromFolder(string path) => CPH.PlaySound(path);
}

public class GiveawayBot
{
    private GiveawayObject giveawayObject;

    private List<Task> tasks = new List<Task>();

    private sendMessageDelegate sendMessage;
    private sendDiscordMessageDelegate sendDiscordMessage;
    private ObsSetGdiTextDelegate obsSetGdiText;
    private ObsSetFilterStateDelegate obsSetFilterStateDelegate;
    private PlaySoundFromFolderDelegate playSoundFromFolderDelegate;
    private ObsSetSourceVisibilityDelegate obsSetSourceVisibilityDelegate;

    private bool requiresGiveawayAPICheck;

    public GiveawayBot(sendMessageDelegate sendMessage
        , sendDiscordMessageDelegate sendDiscordMessage
        , ObsSetGdiTextDelegate obsSetGdiText
        , ObsSetFilterStateDelegate obsSetFilterStateDelegate
        , PlaySoundFromFolderDelegate playSoundFromFolderDelegate
        , ObsSetSourceVisibilityDelegate obsSetSourceVisibilityDelegate
        )
    {
        this.sendMessage = sendMessage;
        this.sendDiscordMessage = sendDiscordMessage;
        this.obsSetGdiText = obsSetGdiText;
        this.obsSetFilterStateDelegate = obsSetFilterStateDelegate;
        this.playSoundFromFolderDelegate = playSoundFromFolderDelegate;
        this.obsSetSourceVisibilityDelegate = obsSetSourceVisibilityDelegate;
    }

    public void startGiveaway(int duration, int reminderTiming, int timeToClaim, Dictionary<string, string> messages, bool requiresGiveawayAPICheck = false)
    {
        // check for if there either hasnt been a giveaway or the old one has been finished already
        if (giveawayObject == null || giveawayObject.hasGiveawayEnded())
        {
            // create a new giveaway object
            giveawayObject = new GiveawayObject(messages["rawInput"], messages["gameId"], messages, requiresGiveawayAPICheck);

            // start new giveaway
            giveawayObject.startGiveaway();

            // set name of new giveaway
            obsSetGdiText("n-GiveAway", "gwyPrizeTXT", giveawayObject.getName());
            obsSetGdiText("n-GiveAway", "gwyEntryCount", "0");
            obsSetSourceVisibilityDelegate("n-GiveAway", "gwyPrizeTXT", true);
            obsSetSourceVisibilityDelegate("n-GiveAway", "gwyEntriesTXT", true);
			obsSetSourceVisibilityDelegate("n-GiveAway", "gwyEntryCount", true);
            // play starting gif
            obsSetSourceVisibilityDelegate("n-GiveAway", "v-gWayVideo", true);
            //play starting sound
            playSoundFromFolderDelegate(giveawayObject.getMessage("giveawayStartSound"));
            Thread.Sleep(500);
            // activate source
            obsSetFilterStateDelegate("n-GiveAwayMOVE", "gWayOn", 0);

            // send starting giveaway message
            int totalMinutes = int.Parse(Math.Floor(duration / 60.0) + "");
            sendMessage("announce", giveawayObject.getMessage("giveawayStartMessage"), true, giveawayObject.getName(), totalMinutes + "");

            // create task to handle reminder times
            Task.Factory.StartNew(() =>
            {
            	double factor = 1.0 - reminderTiming / 1.0 / duration;
                while (true)
                {
                    // wait for defined time until reminding
                    Thread.Sleep(reminderTiming * 1000);

                    // check if the giveaway is still running
                    if (giveawayObject.getStatus().Equals(GiveawayStatus.running))
                    {
                        // send reminder message
                        totalMinutes = int.Parse(Math.Floor(totalMinutes * factor) + "");
                        sendMessage("announce", giveawayObject.getMessage("giveawayReminderMessage"), true, giveawayObject.getName(), totalMinutes + "");
                    }
                    else break;
                }
            });

            // create task to handle giveaway end
            Task.Factory.StartNew(() =>
            {
                // wait for defined time until ending the giveaway
                Thread.Sleep((duration * 1000) - 30000);

                //play Prep Butts sound
                playSoundFromFolderDelegate(giveawayObject.getMessage("giveawayPrepButtsSound"));
                // toggle Filters to show the 30 seconds filter
                obsSetSourceVisibilityDelegate("n-GiveAway", "v-giveAway-30", true);
                obsSetSourceVisibilityDelegate("n-GiveAway", "v-gWayVideo", false);

                // wait for the remaining 30 seconds
                Thread.Sleep(30000);

                // end the giveaway
                giveawayObject.endGiveaway();

                // send giveaway end message
                sendMessage("announce", giveawayObject.getMessage("giveawayClosedMessage"), true, giveawayObject.getName());

                //play winner sound
                playSoundFromFolderDelegate(giveawayObject.getMessage("giveawayWinnerSound"));

                // wait some time for safety
                Thread.Sleep(200);

                // pick the winner and send the winner message
                sendMessage("announce", giveawayObject.getMessage("giveawayWinnerMessage"), true, giveawayObject.pickWinner());
                obsSetSourceVisibilityDelegate("n-GiveAway", "v-giveAway-Winner", true);
                obsSetSourceVisibilityDelegate("n-GiveAway", "v-giveAway-30", false);

                // add new task to check for if the giveaway has been claimed
                Task.Factory.StartNew(() =>
                {
                    // iterate for as long as you have to until someone claims
                    while (true)
                    {
						obsSetSourceVisibilityDelegate("n-GiveAway", "gwyEntriesTXT", false);
						obsSetSourceVisibilityDelegate("n-GiveAway", "gwyEntryCount", false);
                    	obsSetGdiText("n-GiveAway", "gwyWinnerName", giveawayObject.getWinner());
                    	obsSetGdiText("n-GiveAway", "gwyWinnerTime", timeToClaim + "");
                    	
                        // wait for defined time until redrawing the giveaway
                        for (int i = 0; i < timeToClaim; i++)
                        {
                            Thread.Sleep(1000);
                            if (giveawayObject.hasGiveawayEnded())
                            {
                            	obsSetGdiText("n-GiveAway", "gwyWinnerName", "");
								obsSetGdiText("n-GiveAway", "gwyWinnerTime", "");
                                return;
                            }
                            
							obsSetGdiText("n-GiveAway", "gwyWinnerTime", (timeToClaim - (i + 1)) + "");
                        }

                        // send giveaway redraw message
                        sendMessage("announce", giveawayObject.getMessage("giveawayRedrawMessage"), true, giveawayObject.getWinner());
						playSoundFromFolderDelegate(giveawayObject.getMessage("giveawayRedrawSound"));
						
                        // wait some time for safety
                        Thread.Sleep(6000);

                        // remove old winner
                        giveawayObject.removeUser(giveawayObject.getWinner());
                        
                        // pick the winner and send the winner message again
						playSoundFromFolderDelegate(giveawayObject.getMessage("giveawayWinnerSound"));
                        sendMessage("announce", giveawayObject.getMessage("giveawayWinnerMessage"), true, giveawayObject.redraw());
                    }
                });
            });
        }
    }

    public bool claim(string user)
    {
        bool success = giveawayObject.claim(user);
        if (success)
        {
            // send messages to twitch and discord
            sendMessage("announce", giveawayObject.getMessage("giveawayClaimMessage"), true, "@" + user, giveawayObject.getName());
            sendDiscordMessage(giveawayObject.getMessage("giveawayDiscordWebhookLink"), "Twitch Name: @" + user + " - Prize: " + giveawayObject.getName());

            // only call !igs for eve giveaways
            if (giveawayObject.isGame("13263") && !giveawayObject.isApiEnabled())
                sendMessage("message", "!igs " + user, false);

            // disable giveaway overlay
            end();
        }
        return success;
    }

    public bool end()
    {
        StreamerBotHelper.LogDebug("Attempting to stop the giveaway");
        giveawayObject.finishGiveaway();

        // disable giveaway overlay
        obsSetFilterStateDelegate("n-GiveAwayMOVE", "gWayOff", 0);
        Thread.Sleep(5000);
        obsSetSourceVisibilityDelegate("n-GiveAway", "v-giveAway-Winner", false);
        obsSetSourceVisibilityDelegate("n-GiveAway", "gwyPrizeTXT", false);

        return true;
    }

    public bool enter(string name)
    {
        // enter user into giveaway
        bool success = giveawayObject.addUser(name);
        if (success)
        {
            obsSetGdiText("n-GiveAway", "gwyEntryCount", giveawayObject.getparticipantAmount().ToString());
        }
        return success;
    }

    public delegate void sendDiscordMessageDelegate(string webhookUrl, string content);
    public delegate void sendMessageDelegate(string type, string message, bool bot = true, params string[] args);
    public delegate void ObsSetGdiTextDelegate(string scene, string source, string text);
    public delegate void ObsSetFilterStateDelegate(string scene, string filterName, int state);
    public delegate Double PlaySoundFromFolderDelegate(string path);
    public delegate void ObsSetSourceVisibilityDelegate(string scene, string source, bool visible);
}


public static class StreamerBotHelper
{
    private static object _cph;

    public static void Init(object cph)
    {
        _cph = cph;
    }

    public static bool ExecuteMethod(string codeName, string methodName)
    {
        return ((dynamic)_cph).ExecuteMethod(codeName, methodName);
    }

    public static void SendChatMessage(string message)
    {
        ((dynamic)_cph).SendMessage(message);
    }

    public static void LogDebug(string message)
    {
        ((dynamic)_cph).LogDebug(message);
    }

    public static void SetArgument(string argument, object value)
    {
        ((dynamic)_cph).SetArgument(argument, value);
    }
}

public class GiveawayObject
{
    private List<string> participants;
    private string giveawayName;
    public string playedGame;
    private GiveawayStatus status;
    private string winner;
    private Dictionary<string, string> messages;
    private bool requiresGiveawayAPICheck;

    public GiveawayObject(string name, string game, Dictionary<string, string> messages, bool requiresGiveawayAPICheck = false)
    {
        // inititalize vital information
        participants = new List<string>();
        giveawayName = requiresGiveawayAPICheck ? "10 Entries To The !giveaway" : name;
        playedGame = game;
        status = GiveawayStatus.created;
        this.requiresGiveawayAPICheck = requiresGiveawayAPICheck;
        this.messages = messages;
    }

    public void startGiveaway()
    {
        // change giveaway status to running so people can enter
        status = GiveawayStatus.running;
    }

    public void endGiveaway()
    {
        // change giveaway status to waitingForDraw so people cannot enter anymore
        status = GiveawayStatus.waitingForDraw;
    }

    public void finishGiveaway()
    {
        // change giveaway status to waitingForDraw so people cannot enter anymore
        status = GiveawayStatus.ended;
    }

    public bool addUser(string name)
    {
        // check if giveaway is running at the moment
        if (status.Equals(GiveawayStatus.running))
        {
            if ( this.requiresGiveawayAPICheck )
            {
                var contestantResult = StreamerBotHelper.ExecuteMethod("GiveawayAPIHandler", "GetContestantRaw");
                if ( !contestantResult )
                {
                    StreamerBotHelper.SendChatMessage($"{name}, you cannot enter this giveaway as you're not eligible (https://mind1official.com/halp-im-not-eligible/)");
                    return false;
                }
            }

            // check if giveaway already contains user
            if (!participants.Contains(name))
            {
                // add user to giveaway
                participants.Add(name);
                return true;
            }
            else return false;
        }
        else return false;
    }

    public bool removeUser(string name)
    {
        return participants.Remove(name);
    }

    public string pickWinner()
    {
        // check if giveaway is waiting for a draw
        if (status.Equals(GiveawayStatus.waitingForDraw))
        {
            // change status of giveaway to ended so we do not draw another winner
            status = GiveawayStatus.waitingForClaim;

            if (getparticipantAmount() > 0)
            {
                // pick and return random winner
                Random rnd = new Random();
                winner = participants[rnd.Next(participants.Count - 1)];
                return "@" + getWinner();
            }
            else
            {
                finishGiveaway();
                return "No one is registered, ending Giveaway";
            }
        }
        else return "Wrong Giveaway status";
    }

    public string redraw()
    {
        // check if giveaway is waiting for claim
        if (status.Equals(GiveawayStatus.waitingForClaim))
        {
            // reset giveaway to enable redraw
            status = GiveawayStatus.waitingForDraw;
            winner = null;

            // redraw
            return pickWinner();
        }
        else return null;
    }

    public string getWinner()
    {
        // check if giveaway is ended
        if (status.Equals(GiveawayStatus.waitingForClaim))
        {
            // return winner
            return winner;
        }
        else return null;
    }

    public bool claim(string user)
    {
        // check of giveaway is in a claimable state
        if (status.Equals(GiveawayStatus.waitingForClaim))
        {
            // check if the calling user is the winner
            if (getWinner().Equals(user))
            {
                // If we're an API giveaway, send the award right over
                if ( this.requiresGiveawayAPICheck )
                {
                    StreamerBotHelper.SetArgument("entries", 10);
                    StreamerBotHelper.ExecuteMethod("GiveawayAPIHandler","AddEntriesToUser");
                }

                // calling user is the winner, giveaway has ended
                finishGiveaway();
                return true;
            }
            else return false;
        }
        else return false;
    }

    public string getMessage(string identifier) => messages[identifier];

    public int getparticipantAmount() => participants.Count;

    public string getName() => giveawayName;

    public GiveawayStatus getStatus() => status;

    public bool hasGiveawayEnded() => status.Equals(GiveawayStatus.ended);

    public bool isGame(string gameId) => playedGame.Equals(gameId);

    public bool isApiEnabled() => this.requiresGiveawayAPICheck;
}

public enum GiveawayStatus
{
    created,
    running,
    waitingForDraw,
    waitingForClaim,
    ended
}