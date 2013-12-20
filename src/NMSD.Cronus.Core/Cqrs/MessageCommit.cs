﻿using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using NMSD.Cronus.Core.Eventing;
using NMSD.Cronus.Core.Messaging;

namespace NMSD.Cronus.Core.Cqrs
{
    [DataContract(Name = "6a146321-7b94-4ff6-bf2c-cd82cba1836b")]
    public class MessageCommit : IMessage
    {
        MessageCommit()
        {
            UncommittedEvents = new List<object>();
        }

        public MessageCommit(IAggregateRootState state, List<IEvent> events)
        {
            UncommittedState = state;
            UncommittedEvents = events.Cast<object>().ToList();
        }

        [DataMember(Order = 1)]
        private object UncommittedState { get; set; }

        [DataMember(Order = 2)]
        private List<object> UncommittedEvents { get; set; }

        public IAggregateRootState State { get { return (IAggregateRootState)UncommittedState; } }

        public List<IEvent> Events { get { return UncommittedEvents.Cast<IEvent>().ToList(); } }
    }
}