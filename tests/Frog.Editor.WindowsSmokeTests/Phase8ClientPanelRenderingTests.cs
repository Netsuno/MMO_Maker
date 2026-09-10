using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Frog.Client;
using Frog.Client.Controls;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Config;
using Frog.Server.Gameplay;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>Rendering-only panel tests (no network functional evidence).</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class Phase8ClientPanelRenderingTests
{
    [Fact]
    public void Phase8Panels_RenderFromInjectedState_Screenshots()
    {
        StaTestRunner.Run(() =>
        {
            var form = ClientSmokeTestAccess.CreateAndShowMainShell();
            try
            {
                form.SelectPhase8TabForTest();
                form.DialoguePanelForTest.ApplyState(new DialogueStateWire
                {
                    DialogueId = Guid.NewGuid(),
                    PublishedRevision = 1,
                    SessionToken = new byte[Phase8Wire.DialogueSessionTokenBytes],
                    Speaker = "Guide",
                    Text = "Rendering-only dialogue.",
                    Choices = [new DialogueChoiceWire { ChoiceId = "accept", Label = "Accept" }],
                });
                form.QuestJournalPanelForTest.ApplySnapshot(
                [
                    new QuestJournalEntryWire
                    {
                        QuestId = Guid.NewGuid(),
                        Name = "Render quest",
                        Status = (byte)CharacterQuestStatus.Active,
                        StageDescription = "Render",
                        Objectives =
                        [
                            new QuestObjectiveProgressWire
                            {
                                Description = "Render objective",
                                Current = 0,
                                Required = 1,
                            },
                        ],
                    },
                ]);
                form.EnvironmentPanelForTest.ApplyState(new EnvironmentStateWire
                {
                    MapId = 1,
                    RegionId = Guid.NewGuid(),
                    WeatherProfileId = Guid.NewGuid(),
                    LightingLevel = 180,
                });
                ClientSmokeTestAccess.SavePhase8Screenshot(form, "render-panels.png");
            }
            finally
            {
                ClientSmokeTestAccess.CloseMainShell(form);
            }
        });
    }
}
