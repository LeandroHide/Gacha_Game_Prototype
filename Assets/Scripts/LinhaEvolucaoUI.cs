using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Controla uma unica linha do painel de evolucao
public class LinhaEvolucaoUI : MonoBehaviour
{
    public TMP_Text texto;
    public Button botaoEvoluir;

    private string nomeItem;
    private GachaSystem gachaSystem;

    // Configura a linha com referencias necessarias
    public void Configurar(string nome, GachaSystem sistema)
    {
        nomeItem = nome;
        gachaSystem = sistema;

        // Quando clicar no botao, chama OnClickEvoluir
        botaoEvoluir.onClick.AddListener(OnClickEvoluir);
    }

    private void OnClickEvoluir()
    {
        gachaSystem.Evoluir(nomeItem);
        // O GachaSystem atualiza a Pokedex, mas precisamos atualizar a linha tambem
        gachaSystem.AtualizarLinhasEvolucao();
    }

    // Atualiza o texto e o estado do botao baseado nos dados atuais
    public void Atualizar(string nome, string raridade, bool obtido, int nivel, int nivelMax, int fragmentos, int custoProximo, string corHex)
    {
        nomeItem = nome;

        if (!obtido)
        {
            texto.text = "??? (" + raridade + ")";
            botaoEvoluir.interactable = false;
            botaoEvoluir.GetComponentInChildren<TMP_Text>().text = "---";
            return;
        }

        // Monta as estrelas
        string estrelas = "";
        for (int i = 0; i <= nivel; i++) estrelas += "*";

        // Texto da linha
        if (nivel >= nivelMax)
        {
            texto.text = "<color=" + corHex + ">" + nome + "</color> " + estrelas + " (MAX)";
            botaoEvoluir.interactable = false;
            botaoEvoluir.GetComponentInChildren<TMP_Text>().text = "MAX";
        }
        else
        {
            texto.text = "<color=" + corHex + ">" + nome + "</color> " + estrelas + " (frag: " + fragmentos + "/" + custoProximo + ")";

            // Habilita o botao so se tiver fragmentos suficientes
            bool podeEvoluir = fragmentos >= custoProximo;
            botaoEvoluir.interactable = podeEvoluir;
            botaoEvoluir.GetComponentInChildren<TMP_Text>().text = "EVOLUIR POKEMON";
        }
    }
}