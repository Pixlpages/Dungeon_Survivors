using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    float currentEventCooldown = 0;

    public EventData[] events;

    [Tooltip("How long to wait before this becomes active")]
    public float firstTriggerDelay = 180f;

    [Tooltip("How long to wait between each event")]
    public float triggerInterval = 30f;

    public static EventManager instance;

    [System.Serializable]
    public class Event
    {
        public EventData data;
        public float duration, cooldown = 0;
    }
    List<Event> runningEvents = new List<Event>();

    PlayerStats[] allPlayers;

    void Start()
    {
        if (instance)
            Debug.LogWarning("There is more than 1 spawn manager in the scene!");
        instance = this;
        currentEventCooldown = firstTriggerDelay > 0 ? firstTriggerDelay : triggerInterval;
        allPlayers = FindObjectsOfType<PlayerStats>();
    }

    void Awake()
    {
        // Reset event states on scene load to prevent persistence
        runningEvents.Clear();
        currentEventCooldown = firstTriggerDelay > 0 ? firstTriggerDelay : triggerInterval;
        Debug.Log("[EventManager] Reset events on scene load.");
    }

    void Update()
    {
        //cooldown for adding another event to the state
        currentEventCooldown -= Time.deltaTime;
        if (currentEventCooldown <= 0)
        {
            EventData next = null;

            // check MDP first
            if (MDPManager.Instance != null)
                next = MDPManager.Instance.GetPendingEvent();

            // fallback
            if (next == null)
                next = GetRandomEvent();

            //Get an event and try to execute it
            if (next && allPlayers.Length > 0 &&
                next.CheckIfWillHappen(allPlayers[Random.Range(0, allPlayers.Length)]))
            {
                runningEvents.Add(new Event
                {
                    data = next,
                    duration = next.duration
                });
                Debug.Log($"[MDP] Triggered event: {next.name}");
            }

            currentEventCooldown = triggerInterval;
        }

        //Events that we want to remove
        List<Event> toRemove = new List<Event>();

        //cooldown for existing event to see if they should continue running
        foreach (Event e in runningEvents)
        {
            //reduce the current duration
            e.duration -= Time.deltaTime;
            if (e.duration <= 0)
            {
                toRemove.Add(e);
                continue;
            }

            //Reduce the current cooldown
            e.cooldown -= Time.deltaTime;
            if (e.cooldown <= 0)
            {
                //Pick a random player to stic this mob on, then reset the cooldown
                e.data.Activate(allPlayers[Random.Range(0, allPlayers.Length)]);
                e.cooldown = e.data.GetSpawnInterval();
            }
        }

        //Remove all the events that have expired
        foreach (Event e in toRemove)
            runningEvents.Remove(e);
    }

    public EventData GetRandomEvent()
    {
        //If no events are assigned, dont return anything
        if (events.Length <= 0)
            return null;

        //Get a list of all possible events
        List<EventData> possibleEvents = new List<EventData>(events);

        //add the events in event to the possible events only if the event is active
        foreach (EventData e in events)
        {
            if (e.IsActive())
            {
                possibleEvents.Add(e);
            }
        }
        //Randomly pick an event from the possible events to play
        if (possibleEvents.Count > 0)
        {
            EventData result = possibleEvents[Random.Range(0, possibleEvents.Count)];
            return result;
        }
        return null;
    }

}
