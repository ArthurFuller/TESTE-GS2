using UnityEngine;
using UnityEngine.UI;

// A exibição não simula jogadores nem afirma que existe um serviço online.
// A API e a métrica de pontuação ainda precisam ser definidas antes de publicar rankings.
public sealed class GS2LeaderboardView : MonoBehaviour
{
    [SerializeField] private Text status;
    private void OnEnable()
    {
        if (status != null) status.text =
            "Ranking online indisponível. É necessário definir a pontuação da partida " +
            "e integrar um backend com autenticação e validação de resultados.";
    }
}
