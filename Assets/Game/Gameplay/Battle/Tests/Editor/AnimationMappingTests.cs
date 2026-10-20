using System;
using System.IO;
using System.Collections.Generic;
using Game.Presentation.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Tests
{
    public sealed class AnimationMappingTests
    {
        private const string Config = "Assets/Content/Combat/Prototype/presentation.json";
        private const string Alien = "Assets/Content/Units/Heroes/Alien/alien-actions.asset";
        private const string Knight = "Assets/Content/Units/Heroes/Knight/Sprites/knight-actions.asset";

        [Test]
        public void SwappingOnlyTheResourceSelectsItsCompleteAuthoredActionSet()
        {
            var data = BattlePresentationSettings.Parse(File.ReadAllText(Config));
            var alien = AssetDatabase.LoadAssetAtPath<UnitAnimationSet>(Alien);
            var knight = AssetDatabase.LoadAssetAtPath<UnitAnimationSet>(Knight);
            var catalog = new Dictionary<string, UnitAnimationSet>
            {
                { alien.name, alien }, { knight.name, knight }
            };
            var oldPlayer = new UnitAnimationPlayer(catalog[data.Units[0].ResourceId]);
            data.Units[0].ResourceId = knight.name;
            var newPlayer = new UnitAnimationPlayer(catalog[data.Units[0].ResourceId]);
            Assert.That(data.Units[0].ResourceId, Is.EqualTo("knight-actions"));
            Assert.That(oldPlayer.Require(UnitAction.Attack).Frame(0), Is.EqualTo(alien.Frames[6]));
            Assert.That(newPlayer.Require(UnitAction.Attack).Frame(0), Is.EqualTo(knight.Frames[8]));
            Assert.That(oldPlayer.Require(UnitAction.Attack).Fps, Is.EqualTo(20));
            Assert.That(newPlayer.Require(UnitAction.Attack).Fps, Is.EqualTo(10));
            Assert.That(oldPlayer.Find(UnitAction.Skill), Is.Not.Null);
            Assert.That(newPlayer.Find(UnitAction.Skill), Is.Null);
        }

        [Test]
        public void MonstersWithoutSkillsStillProvideRequiredActions()
        {
            var monster = AssetDatabase.LoadAssetAtPath<UnitAnimationSet>("Assets/Content/Units/Enemies/Goblin/Sprites/goblin-actions.asset");
            var player = new UnitAnimationPlayer(monster);
            Assert.That(player.Find(UnitAction.Skill), Is.Null);
            Assert.That(player.Find(UnitAction.Skill2), Is.Null);
            Assert.That(player.Require(UnitAction.Attack), Is.Not.Null);
        }

        [Test]
        public void MissingRequiredWalkIsRejectedBeforePlayback()
        {
            var set = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnitAnimationSet>(Knight));
            try
            {
                set.Walk = null;
                Assert.Throws<FormatException>(() => new UnitAnimationPlayer(set));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(set);
            }
        }

        [Test]
        public void MissingSpritesAndInvalidAuthoredRangesAreRejected()
        {
            var set = ScriptableObject.CreateInstance<UnitAnimationSet>();
            try
            {
                set.Frames = new Sprite[2];
                var action = new UnitActionSettings { FirstFrame = 2, LastFrame = 3, Fps = 12 };
                Assert.Throws<FormatException>(() => new UnitAnimationClip(set, action));
                action.LastFrame = 2;
                Assert.Throws<FormatException>(() => new UnitAnimationClip(set, action));
                action.FirstFrame = int.MaxValue;
                action.LastFrame = int.MaxValue;
                Assert.Throws<FormatException>(() => new UnitAnimationClip(set, action));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(set);
            }
        }

        [Test]
        public void DuplicateUnitBindingsAreRejected()
        {
            var data = BattlePresentationSettings.Parse(File.ReadAllText(Config));
            data.Units[1].UnitKind = data.Units[0].UnitKind;
            Assert.Throws<FormatException>(() => BattlePresentationSettings.Parse(JsonUtility.ToJson(data)));
        }

        [Test]
        public void AllAuthoredResourcesValidateAndSampleTheirStandardActions()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UnitAnimationSet", new[] { "Assets/Content/Units" }))
            {
                var set = AssetDatabase.LoadAssetAtPath<UnitAnimationSet>(AssetDatabase.GUIDToAssetPath(guid));
                var player = new UnitAnimationPlayer(set);
                foreach (UnitAction action in Enum.GetValues(typeof(UnitAction)))
                {
                    var clip = player.Find(action);
                    if (clip == null)
                        continue;
                    Assert.That(clip.Frame(0), Is.Not.Null);
                    Assert.That(clip.Frame(clip.Duration - 0.00001), Is.Not.Null);
                    if (clip.Loop)
                        Assert.That(clip.Frame(clip.Duration + 0.001), Is.EqualTo(clip.Frame(0.001)));
                    else
                        Assert.That(clip.Frame(1000), Is.EqualTo(clip.Frame(clip.Duration - 0.00001)));
                }
            }
        }
    }
}
