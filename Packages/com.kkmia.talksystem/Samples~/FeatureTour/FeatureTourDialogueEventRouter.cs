using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using kkmia.TalkSystem;

[Serializable]
public sealed class FeatureTourEventBinding
{
    public string eventKey;
    public UnityEvent onTriggered;
}

public sealed class FeatureTourDialogueEventRouter : MonoBehaviour
{
    [SerializeField] private List<FeatureTourEventBinding> bindings = new List<FeatureTourEventBinding>();

    private void OnEnable()
    {
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.DialogueEventTriggered += OnDialogueEventTriggered;
    }

    private void OnDisable()
    {
        if (DialogueManager.Instance != null)
            DialogueManager.Instance.DialogueEventTriggered -= OnDialogueEventTriggered;
    }

    private void OnDialogueEventTriggered(DialogueEventContext context)
    {
        if (context == null || string.IsNullOrEmpty(context.EventKey)) return;

        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            if (binding != null && binding.eventKey == context.EventKey && binding.onTriggered != null)
                binding.onTriggered.Invoke();
        }
    }
}
