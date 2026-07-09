using System;
using System.Collections.Generic;
using UnityEngine;

namespace BokeGameJam.Core
{
    /// <summary>
    /// 轻量级全局事件中心，用于解耦简单的玩法/UI 消息。
    /// 用法：EventManager.On("EventName", Handler)、EventManager.Emit("EventName")、EventManager.Off("EventName", Handler)。
    /// </summary>
    public static class EventManager
    {
        private static readonly Dictionary<string, Action> events = new();
        private static readonly Dictionary<string, Delegate> payloadEvents = new();

        public static void On(string eventName, Action listener)
        {
            if (!IsValid(eventName, listener))
                return;

            if (events.TryGetValue(eventName, out Action existingListeners))
                events[eventName] = existingListeners + listener;
            else
                events[eventName] = listener;
        }

        public static void On<T>(string eventName, Action<T> listener)
        {
            if (!IsValid(eventName, listener))
                return;

            if (payloadEvents.TryGetValue(eventName, out Delegate existingListeners))
            {
                if (existingListeners is not Action<T> typedListeners)
                {
                    Debug.LogError($"[EventManager] Event '{eventName}' is already registered with a different payload type.");
                    return;
                }

                payloadEvents[eventName] = typedListeners + listener;
                return;
            }

            payloadEvents[eventName] = listener;
        }

        public static void Off(string eventName, Action listener)
        {
            if (!IsValid(eventName, listener))
                return;

            if (!events.TryGetValue(eventName, out Action existingListeners))
                return;

            Action updatedListeners = existingListeners - listener;
            if (updatedListeners == null)
                events.Remove(eventName);
            else
                events[eventName] = updatedListeners;
        }

        public static void Off<T>(string eventName, Action<T> listener)
        {
            if (!IsValid(eventName, listener))
                return;

            if (!payloadEvents.TryGetValue(eventName, out Delegate existingListeners))
                return;

            if (existingListeners is not Action<T> typedListeners)
            {
                Debug.LogError($"[EventManager] Event '{eventName}' is registered with a different payload type.");
                return;
            }

            Action<T> updatedListeners = typedListeners - listener;
            if (updatedListeners == null)
                payloadEvents.Remove(eventName);
            else
                payloadEvents[eventName] = updatedListeners;
        }

        public static void Emit(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                Debug.LogWarning("[EventManager] Event name is empty.");
                return;
            }

            if (events.TryGetValue(eventName, out Action listeners))
                listeners.Invoke();
        }

        public static void Emit<T>(string eventName, T payload)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                Debug.LogWarning("[EventManager] Event name is empty.");
                return;
            }

            if (!payloadEvents.TryGetValue(eventName, out Delegate listeners))
                return;

            if (listeners is Action<T> typedListeners)
            {
                typedListeners.Invoke(payload);
                return;
            }

            Debug.LogError($"[EventManager] Event '{eventName}' was emitted with the wrong payload type.");
        }

        public static void Clear(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                Debug.LogWarning("[EventManager] Event name is empty.");
                return;
            }

            events.Remove(eventName);
            payloadEvents.Remove(eventName);
        }

        public static void ClearAll()
        {
            events.Clear();
            payloadEvents.Clear();
        }

        private static bool IsValid(string eventName, Delegate listener)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                Debug.LogWarning("[EventManager] Event name is empty.");
                return false;
            }

            if (listener != null)
                return true;

            Debug.LogWarning($"[EventManager] Listener for '{eventName}' is null.");
            return false;
        }
    }
}
