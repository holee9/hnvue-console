global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
global using FluentAssertions;
global using Xunit;
global using HnVue.Workflow.StateMachine;
global using HnVue.Workflow.Journal;
global using HnVue.Workflow.Safety;
global using HnVue.Workflow.Protocol;
global using HnVue.Workflow.Study;
global using HnVue.Workflow.Interfaces;
global using HnVue.Workflow.Recovery;
// Type alias to resolve Protocol namespace/type ambiguity
using Protocol = HnVue.Workflow.Protocol.Protocol;
