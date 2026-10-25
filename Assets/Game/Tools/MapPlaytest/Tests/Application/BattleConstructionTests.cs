using System.IO;
using System.Linq;
using System.Reflection;
using Game.Battle.Contracts.Commands;
using Game.Battle.Contracts.Ports;
using Game.Battle.Domain.Definitions;
using Game.Battle.Simulation.Bridges;
using Game.Construction.Domain.Definitions;
using Game.Infrastructure.Configuration;
using Game.Infrastructure.Maps.Serialization;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using Game.Tools.MapPlaytest.Application.Sessions;
using NUnit.Framework;
using Game.Tools.MapPlaytest.Unity.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tools.MapPlaytest.Tests
{
    public sealed class BattleConstructionTests
    {
        private const string Root = "Assets/Content/Configuration/Resources/GameConfig/";
        private sealed class Input : IHeroInput { public MoveIntent Read() => default; }
        private static MapPlaySession Session(int gold = 1000)
        {
            var catalog = FacilityCatalogJson.Read(File.ReadAllText(Root + "facilities.json"));
            // 通用建设规则仍验证装饰和敌台；测试夹具不要求实际游戏继续出售这些内容。
            catalog = new MapContentCatalog(catalog.FacilityIds.Select(catalog.Find).Concat(new[]
            {
                new FacilityDefinition { id = "market", name = "Test decoration", hasFunction = false },
                new FacilityDefinition { id = "bastion", name = "Test bastion", wallMounted = true }
            }), catalog.Wall);
            var content = JsonConstructionContentSource.Parse(File.ReadAllText(Root + "construction-content.json"));
            content = new ConstructionContentDefinition(content.Buildings.Values.Concat(new[]
            {
                new BuildingDevelopmentDefinition("market", 0, 0, false, 0, BuildingCategory.Decoration),
                new BuildingDevelopmentDefinition("bastion", 100, 0, true, 0.5f,
                    BuildingCategory.PlayerBuildable, ConstructionPlacementKind.Bastion)
            }), content.Upgrades.Values, content.Barracks.Values);
            var map = new MapLayout();
            map.markers.Add(new MapMarker { id = "hero", name = "Hero", kind = MarkerKind.HeroSpawn, x = -9.5f, y = 6.5f });
            map.markers.Add(new MapMarker { id = "entry", name = "Entry", kind = MarkerKind.EnemyEntry, x = 10.5f, y = 6.5f });
            map.facilities.Add(new InitialFacility { id = "castle", definitionId = "hall", x = 1, y = 1 });
            map.facilities.Add(new InitialFacility { id = "market", definitionId = "market", x = 5, y = 10 });
            var session = new MapPlaySession(new MapDefinition(map, catalog), catalog,
                JsonBattleDefinitionSource.Parse(File.ReadAllText(Root + "battle.json")), new Input(), () => new EcsBattleSimulation(),
                construction: content, initialGold: gold);
            session.Restart();
            return session;
        }

        [Test]
        public void Build_MenuAndCommandRejectDecorationAndBuiltIn_AndDecorationHasNoCombatEntity()
        {
            using var session = Session();
            Assert.That(session.BuildableBuildings.All(item => item.Category == BuildingCategory.PlayerBuildable), Is.True);
            Assert.That(session.BuildableBuildings.Any(item => item.Id == "market" || item.Id == "hall"), Is.False);
            foreach (string id in new[] { "farm", "well", "church", "market", "hall" })
                Assert.That(session.TryBuild(id, 10, 8, out _), Is.False, id);
            Assert.That(session.Structures.Any(item => item.Id == "market"), Is.False);
            Assert.That(session.TrySellBuilding("market", out _), Is.False);
            Assert.That(session.TrySellBuilding("castle", out _), Is.False);
            Assert.That(session.Gold, Is.EqualTo(1000));
        }

        [Test]
        public void Preview_IsReadOnly_AndConfirmationChecksWholeFootprintAndGold()
        {
            using var session = Session();
            int version = session.MapVersion;
            Assert.That(session.PreviewBuild("barracks.melee", 10, 8, out _), Is.True);
            Assert.That(session.MapVersion, Is.EqualTo(version));
            Assert.That(session.Gold, Is.EqualTo(1000));
            Assert.That(session.PreviewBuild("barracks.melee", 23, 8, out _), Is.False);
            Assert.That(session.TryBuild("barracks.melee", 10, 8, out _), Is.True);
            Assert.That(session.TryBuild("tower", 11, 9, out _), Is.False);
            Assert.That(session.Gold, Is.EqualTo(900));
            Assert.That(session.Snapshot.facilities.Count, Is.EqualTo(3));
        }

        [Test]
        public void Build_InsufficientFunds_DoesNotCreateBuildingOrChangeVersion()
        {
            using var session = Session(10);
            int version = session.MapVersion;
            Assert.That(session.TryBuild("tower", 10, 8, out _), Is.False);
            Assert.That(session.MapVersion, Is.EqualTo(version));
            Assert.That(session.Gold, Is.EqualTo(10));
        }

        [Test]
        public void BuildBarracks_BlockedBottomExit_RequiresAnotherTileAndAlwaysDeploysBelow()
        {
            using var session = Session();
            Assert.That(session.PreviewBuild("barracks.melee", 10, 0, out _), Is.False);
            Assert.That(session.TryBuild("barracks.melee", 10, 0, out _), Is.False);
            Assert.That(session.Gold, Is.EqualTo(1000));
            Assert.That(session.TryBuild("barracks.melee", 10, 1, out var reason), Is.True, reason);
            Assert.That(session.Gold, Is.EqualTo(900));
            var soldiers = session.Units.Where(unit => Game.Battle.Domain.ValueObjects.UnitKinds.IsSoldier(unit.Kind)).ToArray();
            Assert.That(soldiers, Is.Not.Empty);
            Assert.That(soldiers.All(unit => unit.Y < -7), Is.True, "士兵应从兵营下方出营");
        }

        [Test]
        public void Upgrade_ExclusiveBranchAndCastle_ApplyWithoutChangingFootprint_AndRestartClearsInvestment()
        {
            using var session = Session();
            Assert.That(session.TryBuild("tower", 10, 8, out _), Is.True);
            var tower = session.Snapshot.facilities.Single(item => item.definitionId == "tower");
            Assert.That(session.TryUpgradeBuilding(tower.id, "upgrade.tower.rapid", out _), Is.True);
            Assert.That(session.TryUpgradeBuilding(tower.id, "upgrade.tower.heavy", out _), Is.False);
            Assert.That(session.BuildingLevel(tower.id), Is.EqualTo(2));
            Assert.That(session.Structures.Single(item => item.Id == tower.id).Health, Is.EqualTo(175));
            Assert.That(session.TryUpgradeBuilding("castle", "upgrade.hall.2", out _), Is.True);
            Assert.That(session.Structures.Single(item => item.Id == "castle").Health, Is.EqualTo(750));
            Assert.That(session.Snapshot.facilities.Single(item => item.id == tower.id).x, Is.EqualTo(10));
            session.Restart();
            Assert.That(session.Structures.Single(item => item.Id == "castle").Health, Is.EqualTo(500));
            Assert.That(session.BuildingLevel("castle"), Is.EqualTo(1));
            Assert.That(session.Gold, Is.EqualTo(1000));
        }

        [Test]
        public void Sell_PaidInvestmentIncludesUpgrade_AndCannotRefundTwice()
        {
            using var session = Session();
            session.TryBuild("tower", 10, 8, out _);
            var tower = session.Snapshot.facilities.Single(item => item.definitionId == "tower");
            session.TryUpgradeBuilding(tower.id, "upgrade.tower.rapid", out _);
            Assert.That(session.QuoteBuildingSale(tower.id, out int refund, out _), Is.True);
            Assert.That(refund, Is.EqualTo(62));
            Assert.That(session.TrySellBuilding(tower.id, out _), Is.True);
            Assert.That(session.Gold, Is.EqualTo(937));
            Assert.That(session.TrySellBuilding(tower.id, out _), Is.False);
            Assert.That(session.Gold, Is.EqualTo(937));
            Assert.That(session.Structures.Any(item => item.Id == tower.id), Is.False);
        }

        [Test]
        public void SellBarracks_RetiresOwnedSoldiers_AndNeverProducesFreeArmy()
        {
            using var session = Session();
            Assert.That(session.TryBuild("barracks.melee", 10, 8, out _), Is.True);
            var barracks = session.Snapshot.facilities.Single(item => item.definitionId == "barracks.melee");
            Assert.That(session.Units.Any(item => Game.Battle.Domain.ValueObjects.UnitKinds.IsSoldier(item.Kind)), Is.True);
            Assert.That(session.TrySellBuilding(barracks.id, out _), Is.True);
            Assert.That(session.Units.Any(item => Game.Battle.Domain.ValueObjects.UnitKinds.IsSoldier(item.Kind)), Is.False);
            Assert.That(session.GetBarracksStatuses(), Is.Empty);
        }

        [Test]
        public void WallGateAndPartialSale_KeepRemaindersAndRejectHalfGate_RefundOnlyRemovedInvestment()
        {
            using var session = Session();
            Assert.That(session.TryBuildWall(new MapPoint(-4, -3), new MapPoint(6, -3), out var reason), Is.True, reason);
            string edgeId = session.Snapshot.wallEdges.Single().id;
            Assert.That(session.TryPlaceGate(edgeId, 2, 4, 2, false, out string gateId, out reason), Is.True, reason);
            Assert.That(session.Gold, Is.EqualTo(840));
            int version = session.MapVersion;
            Assert.That(session.TrySellWall(edgeId, 1, 3, out _), Is.False);
            Assert.That(session.MapVersion, Is.EqualTo(version));
            Assert.That(session.TrySellWall(edgeId, 2, 6, out reason), Is.True, reason);
            Assert.That(session.Snapshot.wallEdges.Count, Is.EqualTo(2));
            Assert.That(session.Snapshot.wallEdges.All(item => item.gates.Count == 0), Is.True);
            Assert.That(session.Gold, Is.EqualTo(890));
            Assert.That(session.TrySellWall(edgeId, 2, 6, out _), Is.False);
            foreach (var edge in session.Snapshot.wallEdges)
                Assert.That(session.TrySellWall(edge.id, 0, session.WallLengthTiles(edge.id), out reason), Is.True, reason);
            Assert.That(session.Gold, Is.EqualTo(920));
        }

        [Test]
        public void Bastion_IsIndependentBuilding_AndSaleRequiresExplicitRemovalOfConnectedWalls()
        {
            using var session = Session();
            session.TryBuildWall(new MapPoint(-4, -3), new MapPoint(6, -3), out _);
            string nodeId = session.Snapshot.wallNodes.First().id;
            Assert.That(session.TryBuildBastion(nodeId, out var reason), Is.True, reason);
            var bastion = session.Snapshot.facilities.Single(item => item.definitionId == "bastion");
            Assert.That(session.TryBuildBastion(nodeId, out _), Is.False);
            Assert.That(session.TrySellBuilding(bastion.id, out _), Is.False);
            string edgeId = session.Snapshot.wallEdges.Single().id;
            Assert.That(session.TrySellWall(edgeId, 0, 10, out reason), Is.True, reason);
            Assert.That(session.TrySellBuilding(bastion.id, out reason), Is.True, reason);
        }

        [Test]
        public void Bastion_InMiddleOfWall_SplitsOnlyOnCommitAndPreservesWallInvestment()
        {
            using var session = Session();
            session.TryBuildWall(new MapPoint(-4, -3), new MapPoint(6, -3), out _);
            Assert.That(session.PreviewBastionAt(new MapPoint(1, -3), out var preview, out var reason), Is.True, reason);
            Assert.That(preview, Is.Not.Null);
            Assert.That(session.Snapshot.wallEdges.Count, Is.EqualTo(1));
            Assert.That(session.Gold, Is.EqualTo(900));
            Assert.That(session.TryBuildBastionAt(new MapPoint(1, -3), out reason), Is.True, reason);
            Assert.That(session.Snapshot.wallEdges.Count, Is.EqualTo(2));
            Assert.That(session.Gold, Is.EqualTo(800));
            foreach (var edge in session.Snapshot.wallEdges)
                Assert.That(session.TrySellWall(edge.id, 0, session.WallLengthTiles(edge.id), out reason), Is.True, reason);
            Assert.That(session.Gold, Is.EqualTo(850));
        }

        [Test]
        public void Rectangle_UsesConfiguredWidthAndHeightWithoutSwapping()
        {
            var catalog = new MapContentCatalog(new[] { new FacilityDefinition { id = "rectangle", width = 3, height = 2 } });
            var state = new Game.Construction.Application.Sessions.MapConstructionState(new MapDefinition(new MapLayout(), catalog), catalog);
            Assert.That(state.TryBuild("rectangle", 22, 8, _ => true, out _), Is.False);
            Assert.That(state.TryBuild("rectangle", 21, 8, _ => true, out var reason), Is.True, reason);
            var building = state.Snapshot.facilities.Single();
            Assert.That(building.Width(catalog.Find("rectangle")), Is.EqualTo(3));
            Assert.That(building.Height(catalog.Find("rectangle")), Is.EqualTo(2));
            Assert.That(state.Navigation.CanOccupy(11.5f, 1.5f, 0.1f), Is.False);
            Assert.That(state.Navigation.CanOccupy(11.5f, 2.5f, 0.1f), Is.True);
        }

        [Test]
        public void TileMenu_DisablesBuildingsThatDoNotFit_AndDoesNotOfferWallUpgrades()
        {
            using var session = Session();
            WithConstructionView(session, view =>
            {
                InvokeView(view, "OpenContext", new MapPoint(11.5f, -2.5f), Vector2.zero);
                var choices = MenuButtons(view);
                Assert.That(choices.Single(button => button.text.StartsWith("游侠箭楼")).enabledSelf, Is.True);
                var barracks = choices.Single(button => button.text.StartsWith("近战兵营"));
                Assert.That(barracks.enabledSelf, Is.False);
                Assert.That(barracks.tooltip, Does.Contain("超出地图"));
                Assert.That(choices.Any(button => button.text.Contains("城门") || button.text.Contains("敌台")), Is.False);
                Assert.That(choices.Any(button => button.text.Contains("旋转")), Is.False);
            });
        }

        [Test]
        public void TileMenu_MovingPointerDoesNotMoveChosenBuilding_AndSuccessfulConfirmationExitsMode()
        {
            using var session = Session();
            WithConstructionView(session, view =>
            {
                InvokeView(view, "OpenContext", new MapPoint(-1.5f, 0.5f), Vector2.zero);
                InvokeView(view, "UpdatePointer", session.Snapshot, new MapPoint(6, 3));
                ActivateButton(MenuButtons(view).Single(button => button.text.StartsWith("游侠箭楼")));
                Assert.That(MenuButtons(view).Any(button => button.text.Contains("旋转")), Is.False);
                InvokeView(view, "UpdatePointer", session.Snapshot, new MapPoint(-6, 4));
                ActivateButton(MenuButtons(view).Single(button => button.text == "确认建设"));
                var tower = session.Snapshot.facilities.Single(item => item.definitionId == "tower");
                Assert.That(tower.x, Is.EqualTo(10));
                Assert.That(tower.y, Is.EqualTo(8));
                Assert.That(session.Gold, Is.EqualTo(950));
                Assert.That(view.Active, Is.False);
            });
        }

        [Test]
        public void FixedBuildingConfirmation_RechecksOccupiedSpace_AndFailureKeepsModeWithoutPayment()
        {
            using var session = Session();
            WithConstructionView(session, view =>
            {
                InvokeView(view, "OpenContext", new MapPoint(-1.5f, 0.5f), Vector2.zero);
                ActivateButton(MenuButtons(view).Single(button => button.text.StartsWith("游侠箭楼")));
                Assert.That(session.TryBuild("tower", 10, 8, out _), Is.True);
                ActivateButton(MenuButtons(view).Single(button => button.text == "确认建设"));
                Assert.That(session.Gold, Is.EqualTo(950));
                Assert.That(session.Snapshot.facilities.Count(item => item.definitionId == "tower"), Is.EqualTo(1));
                Assert.That(view.Active, Is.True);
            });
        }

        [Test]
        public void WallMenu_OffersGateAndHealthOperations_WithoutUnsupportedBastion()
        {
            using var session = Session();
            session.TryBuildWall(new MapPoint(-4, -3), new MapPoint(6, -3), out _);
            WithConstructionView(session, view =>
            {
                InvokeView(view, "OpenContext", new MapPoint(1, -3), Vector2.zero);
                Assert.That(MenuButtons(view).Any(button => button.text.Contains("建设城门")), Is.True);
                Assert.That(MenuButtons(view).Any(button => button.text.StartsWith("建设敌台")), Is.False);
                Assert.That(MenuButtons(view).Any(button => button.text.Contains("造成 30%")), Is.True);
            });
        }

        private static void WithConstructionView(MapPlaySession session, System.Action<BattleConstructionView> verify)
        {
            var host = new GameObject("Construction UI test");
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            try
            {
                var hud = host.AddComponent<UIDocument>();
                hud.panelSettings = panel;
                hud.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Game/Tools/MapPlaytest/Unity/UI/MapPlayHud.uxml");
                var view = host.AddComponent<BattleConstructionView>();
                var catalog = FacilityCatalogJson.Read(File.ReadAllText(Root + "facilities.json"));
                view.Initialize(session, catalog, null, null, hud, () => { }, _ => { });
                view.SetActive(true);
                verify(view);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(panel);
            }
        }

        private static Button[] MenuButtons(BattleConstructionView view) =>
            ((VisualElement)typeof(BattleConstructionView).GetField("_menu", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(view)).contentContainer.Children().OfType<Button>().ToArray();

        private static void ActivateButton(Button button)
        {
            Assert.That(button.enabledInHierarchy, Is.True, button.text);
            // EditMode 文档未挂运行面板，直接激活 Clickable 的真实回调，不伪造建设命令。
            typeof(Clickable).GetMethod("Invoke", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(button.clickable, new object[] { null });
        }

        private static void InvokeView(BattleConstructionView view, string method, params object[] arguments) =>
            typeof(BattleConstructionView).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, arguments);
    }
}
