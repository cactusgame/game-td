using UnityEngine.Serialization;
using Game.Battle.Application.Sessions;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.Presentation.UI
{
    /// <summary>读取一局的只读统计并刷新 HUD；唯一业务请求是把 Restart 点击交给应用层。</summary>
    public sealed class BattleHud : MonoBehaviour
    {
        [FormerlySerializedAs("status")]
        [SerializeField]
        private TMP_Text _status;
        [FormerlySerializedAs("heroHealth")]
        [SerializeField]
        private UnityEngine.UI.Image _heroHealth;
        [FormerlySerializedAs("restart")]
        [SerializeField]
        private UnityEngine.UI.Button _restart;
        private BattleSession _session;
        private float _next;
        public void Configure(TMP_Text text, UnityEngine.UI.Image health, UnityEngine.UI.Button button)
        {
            _status = text;
            _heroHealth = health;
            _restart = button;
        }

        // 同一场景的 HUD、视图和入口获得同一个 BattleSession，不各自创建一局。
        [Inject]
        public void Initialize(BattleSession session)
        {
            _session = session;
            _restart.onClick.AddListener(Restart);
        }

        private void Restart()
        {
            _session.Restart();
        }

        private void Update()
        {
            if (_session?.Definition == null || Time.unscaledTime < _next)
                return;
            // 文本每秒最多刷新十次，减少字符串构建与 Canvas 重建；不影响固定步模拟频率。
            _next = Time.unscaledTime + 0.1f;
            _heroHealth.fillAmount = (float)_session.HeroHealth / _session.Definition.Hero.MaxHealth;
            string message;
            if (_session.HeroHealth == 0)
            {
                message = "HERO FALLEN  /  RESTART TO RETRY";
            }
            else if (_session.AliveMonsters == 0)
            {
                message = "FIELD CLEARED";
            }
            else
            {
                message = "CONTACT TO ATTACK";
            }
            _status.text = $"<b>KNIGHT</b>  {_session.HeroHealth}/{_session.Definition.Hero.MaxHealth} HP\n" + $"MONSTERS  {_session.AliveMonsters}/{_session.Definition.MonsterCount}    CONTACTS  {_session.ContactCount}\n" + $"<color=#C9BC8F>{message}</color>";
        }

        private void OnDestroy()
        {
            if (_restart != null)
                _restart.onClick.RemoveListener(Restart);
        }
    }
}
