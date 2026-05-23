using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GachaSystem : MonoBehaviour
{
    [System.Serializable]
    public class GachaItem
    {
        public string nome;
        public string raridade;
        public float peso;
        public Sprite sprite;
    }

    [System.Serializable]
    public class Banner
    {
        public string nome;
        public List<string> itensNomes = new List<string>();
        public int custoPull1 = 100;
        public int custoPull10 = 900;
        public int pityLimite = 50;
        public string raridadeGarantida = "Lendario";
    }

    public List<GachaItem> itens = new List<GachaItem>();
    public List<Banner> banners = new List<Banner>();

    public TMP_Text resultText;
    public TMP_Text pokedexText;
    public GameObject darkOverlay;

    public UnityEngine.UI.Image imagemPull;
    public UnityEngine.UI.Image fundoPainelRevelacao;
    // Audio
    public AudioSource audioSourceEfeitos;
    public AudioClip somClique;
    public AudioClip somPull;
    public AudioClip somEpico;
    public AudioClip somLendario;
    public GameObject painelRevelacao;
    public UnityEngine.UI.Image imagemRevelacao;
    public TMP_Text textoRevelacao;
    public GameObject painelSelecaoBanner;
    public Button botaoPull;
    public Button botaoPull10;
    public Button botaoReset;
    public Button botaoGemas;
    public Button botaoAbrirEvolucao;
    public GameObject particulaBrilhoTemplate;
    public Button botaoTrocarBanner;
    public GameObject painelEvolucao;
    public Transform containerLista;
    public GameObject prefabLinha;
    public Button botaoVoltar;

    public int gemasIniciais = 10000;
    public int gemasPorRecarga = 1000;

    public int nivelMaximo = 5;
    public int[] custoPorNivel = new int[] { 1, 2, 3, 4, 5 };

    // Estado global (Pokedex, fragmentos, niveis, gemas)
    private int gemas = 0;
    private Dictionary<string, bool> obtido = new Dictionary<string, bool>();
    private Dictionary<string, int> nivel = new Dictionary<string, int>();
    private Dictionary<string, int> fragmentos = new Dictionary<string, int>();

    // Estado por banner (pulls e pity separados)
    private Dictionary<string, int> totalPullsPorBanner = new Dictionary<string, int>();
    private Dictionary<string, int> pullsSemRaridadePorBanner = new Dictionary<string, int>();

    // Banner atualmente selecionado (publico pra Sub-etapa B poder mudar)
    public int bannerAtualIndex = 0;

    private bool ultimoPullFoiPity = false;
    private bool animandoPull = false;

    private List<LinhaEvolucaoUI> linhasInstanciadas = new List<LinhaEvolucaoUI>();

    void Start()
    {
        foreach (var item in itens)
        {
            obtido[item.nome] = false;
            nivel[item.nome] = 0;
            fragmentos[item.nome] = 0;
        }

        foreach (var banner in banners)
        {
            totalPullsPorBanner[banner.nome] = 0;
            pullsSemRaridadePorBanner[banner.nome] = 0;
        }

        gemas = gemasIniciais;

        Carregar();
        AtualizarPokedex();

        if (darkOverlay != null) darkOverlay.SetActive(false);
        if (painelEvolucao != null) painelEvolucao.SetActive(false);

        CriarLinhasEvolucao();
    }

    // Retorna o banner atualmente ativo
    private Banner BannerAtual()
    {
        if (banners.Count == 0) return null;
        if (bannerAtualIndex < 0 || bannerAtualIndex >= banners.Count) bannerAtualIndex = 0;
        return banners[bannerAtualIndex];
    }

    // Filtra os itens que estao no banner atual
    private List<GachaItem> ItensDoBannerAtual()
    {
        Banner b = BannerAtual();
        List<GachaItem> filtrados = new List<GachaItem>();

        if (b == null) return itens;

        foreach (var nome in b.itensNomes)
        {
            foreach (var item in itens)
            {
                if (item.nome == nome)
                {
                    filtrados.Add(item);
                    break;
                }
            }
        }

        return filtrados;
    }

    // Muda o banner ativo (chamado pela tela de selecao na Sub-etapa B)
    public void SelecionarBanner(int index)
{
    TocarSom(somClique);
    if (index < 0 || index >= banners.Count) return;
    bannerAtualIndex = index;
    AtualizarPokedex();
    Salvar();
    
    // Fecha o painel de selecao
    if (painelSelecaoBanner != null) painelSelecaoBanner.SetActive(false);
    
    Debug.Log("Banner ativo: " + banners[index].nome);
}

    // Abre o painel de selecao pra trocar de banner
    public void AbrirSelecaoBanner()
{
    if (animandoPull) return;
    TocarSom(somClique);
    if (painelSelecaoBanner != null) painelSelecaoBanner.SetActive(true);
}

    public void Puxar()
    {
        if (animandoPull) return;

        TocarSom(somClique);

        Banner b = BannerAtual();
        if (b == null) return;

        if (gemas < b.custoPull1)
        {
            resultText.text = "Gemas insuficientes!\n\nVoce tem " + gemas + " gemas.\nPrecisa de " + b.custoPull1 + ".";
            return;
        }

        gemas -= b.custoPull1;
        GachaItem itemSorteado = ExecutarPullSemMarcar();

        StartCoroutine(AnimarPullUnico(itemSorteado));
    }

    public void Puxar10()
    {
        if (animandoPull) return;

        TocarSom(somClique);

        Banner b = BannerAtual();
        if (b == null) return;

        if (gemas < b.custoPull10)
        {
            resultText.text = "Gemas insuficientes!\n\nVoce tem " + gemas + " gemas.\nPrecisa de " + b.custoPull10 + ".";
            return;
        }

        gemas -= b.custoPull10;

        List<GachaItem> resultados = new List<GachaItem>();
        List<bool> pityFlags = new List<bool>();
        List<bool> novoFlags = new List<bool>();

        for (int i = 0; i < 10; i++)
        {
            GachaItem item = ExecutarPullSemMarcar();
            bool eraNovo = !obtido[item.nome];
            AplicarPullNoInventario(item);
            resultados.Add(item);
            pityFlags.Add(ultimoPullFoiPity);
            novoFlags.Add(eraNovo);
        }

        StartCoroutine(AnimarPull10(resultados, pityFlags, novoFlags));
    }

    private void CriarLinhasEvolucao()
    {
        if (prefabLinha == null || containerLista == null) return;

        foreach (var item in itens)
        {
            GameObject linha = Instantiate(prefabLinha, containerLista);
            LinhaEvolucaoUI script = linha.GetComponent<LinhaEvolucaoUI>();
            script.Configurar(item.nome, this);
            linhasInstanciadas.Add(script);
        }

        AtualizarLinhasEvolucao();
    }

    public void AtualizarLinhasEvolucao()
    {
        for (int i = 0; i < itens.Count && i < linhasInstanciadas.Count; i++)
        {
            var item = itens[i];
            var linha = linhasInstanciadas[i];

            int custoProximo = (nivel[item.nome] < nivelMaximo) ? custoPorNivel[nivel[item.nome]] : 0;
            string corHex = CorPorRaridade(item.raridade);

            linha.Atualizar(item.nome, item.raridade, obtido[item.nome], nivel[item.nome], nivelMaximo, fragmentos[item.nome], custoProximo, corHex);
        }
    }

    public void AbrirEvolucao()
    {
        if (animandoPull) return;
        TocarSom(somClique);
        AtualizarLinhasEvolucao();
        if (painelEvolucao != null) painelEvolucao.SetActive(true);
    }

    public void FecharEvolucao()
    {
        TocarSom(somClique);
        if (painelEvolucao != null) painelEvolucao.SetActive(false);
        AtualizarPokedex();
    }

    public bool Evoluir(string nomeItem)
    {   
        TocarSom(somClique);
        if (!nivel.ContainsKey(nomeItem)) return false;
        if (nivel[nomeItem] >= nivelMaximo) return false;
        if (fragmentos[nomeItem] < custoPorNivel[nivel[nomeItem]]) return false;

        fragmentos[nomeItem] -= custoPorNivel[nivel[nomeItem]];
        nivel[nomeItem]++;
        Salvar();
        AtualizarPokedex();
        return true;
    }

    private IEnumerator AnimarPullUnico(GachaItem item)
    {
        animandoPull = true;
        DesabilitarBotoes();
        TocarSom(somPull);

        if (darkOverlay != null) darkOverlay.SetActive(true);

        resultText.text = "Sorteando...";
        yield return new WaitForSeconds(0.6f);

        if (item.raridade == "Lendario")
        {
            TocarSom(somLendario);
            resultText.text = "*** *** ***";
            yield return new WaitForSeconds(0.4f);
            resultText.text = "ALGO RARO!";
            yield return new WaitForSeconds(0.6f);
        }
        else if (item.raridade == "Epico")
        {
            TocarSom(somEpico);
            resultText.text = "*** ***";
            yield return new WaitForSeconds(0.6f);
        }
        else
        {
            resultText.text = "...";
            yield return new WaitForSeconds(0.4f);
        }

        string corHex = CorPorRaridade(item.raridade);
        string mensagemPity = ultimoPullFoiPity ? "<color=#FFD700>PITY GARANTIU!</color>\n\n" : "";

        bool eraNovo = !obtido[item.nome];
        AplicarPullNoInventario(item);

        string statusItem = eraNovo
            ? "<color=#00FF00>NOVO!</color>"
            : "<color=#AAAAAA>Duplicata -> +1 fragmento</color>";

        Banner b = BannerAtual();
        int totalDoBanner = totalPullsPorBanner[b.nome];

        // Mostra o painel de revelacao em tela cheia
        if (painelRevelacao != null)
        {
            if (imagemRevelacao != null && item.sprite != null)
            {
                imagemRevelacao.sprite = item.sprite;
            }

            if (textoRevelacao != null)
            {
                textoRevelacao.text = mensagemPity +
                    "<color=" + corHex + "><size=140%>" + item.nome + "</size></color>\n" +
                    "<color=" + corHex + ">(" + item.raridade + ")</color>\n\n" +
                    statusItem;
            }

            ColorirFundoPorRaridade(item.raridade);
            painelRevelacao.SetActive(true);
            StartCoroutine(AnimarZoomImagem());
            // Particulas douradas pra Lendario
            if (item.raridade == "Lendario")
            {
                GerarParticulasLendario();
            }
        }

        // Atualiza tambem o resultText (pra historico)
        resultText.text = "Pull #" + totalDoBanner + " (" + b.nome + ")\n\nUltimo: " + item.nome + " (" + item.raridade + ")";

        yield return new WaitForSeconds(0.4f);

        if (darkOverlay != null) darkOverlay.SetActive(false);

        Salvar();
        AtualizarPokedex();
        HabilitarBotoes();
        animandoPull = false;
    }

    private IEnumerator AnimarPull10(List<GachaItem> resultados, List<bool> pityFlags, List<bool> novoFlags)
    {
        animandoPull = true;
        DesabilitarBotoes();

        if (darkOverlay != null) darkOverlay.SetActive(true);

        resultText.text = "Sorteando x10...";
        yield return new WaitForSeconds(0.8f);

        for (int i = 0; i < resultados.Count; i++)
        {
            GachaItem item = resultados[i];

            if (item.raridade == "Epico" || item.raridade == "Lendario")
            {
                // Toca som especifico da raridade
                if (item.raridade == "Lendario")
                    TocarSom(somLendario);
                else
                    TocarSom(somEpico);

                // Esconde o overlay escuro pra nao competir com o painel de revelacao
                if (darkOverlay != null) darkOverlay.SetActive(false);

                // Mostra a tela de revelacao do monstro
                if (painelRevelacao != null)
                {
                    if (imagemRevelacao != null && item.sprite != null)
                    {
                        imagemRevelacao.sprite = item.sprite;
                    }

                    if (textoRevelacao != null)
                    {
                        string cor = CorPorRaridade(item.raridade);
                        string marcadorPity = pityFlags[i] ? " <color=#FFD700>[PITY!]</color>" : "";
                        string marcadorNovo = novoFlags[i] ? " <color=#00FF00>[NOVO!]</color>" : "";

                        textoRevelacao.text =
                            "Pull " + (i + 1) + " de 10\n\n" +
                            "<color=" + cor + "><size=140%>" + item.nome + "</size></color>\n" +
                            "<color=" + cor + ">(" + item.raridade + ")</color>" +
                            marcadorPity + marcadorNovo;
                    }

                    ColorirFundoPorRaridade(item.raridade);
                    painelRevelacao.SetActive(true);
                    StartCoroutine(AnimarZoomImagem());

                    // Particulas douradas pra Lendario
                    if (item.raridade == "Lendario")
                    {
                        GerarParticulasLendario();
                    }
                }

                // Espera o jogador fechar o painel pra continuar
                while (painelRevelacao != null && painelRevelacao.activeSelf)
                {
                    yield return null;
                }

                // Reativa o overlay pro proximo pull (se houver)
                if (darkOverlay != null && i < resultados.Count - 1) darkOverlay.SetActive(true);
            }
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== PULL x10 ===");
        sb.AppendLine("");

        for (int i = 0; i < resultados.Count; i++)
        {
            GachaItem item = resultados[i];
            string cor = CorPorRaridade(item.raridade);
            string marcadorPity = pityFlags[i] ? " [PITY!]" : "";
            string marcadorNovo = novoFlags[i] ? " [NOVO!]" : "";

            sb.AppendLine((i + 1) + ". <color=" + cor + ">" + item.nome + " (" + item.raridade + ")</color>" + marcadorPity + marcadorNovo);
        }

        resultText.text = sb.ToString();
        yield return new WaitForSeconds(0.4f);

        if (darkOverlay != null) darkOverlay.SetActive(false);

        Salvar();
        AtualizarPokedex();
        HabilitarBotoes();
        animandoPull = false;
    }

    private string CorPorRaridade(string raridade)
    {
        if (raridade == "Lendario") return "#FFD700";
        if (raridade == "Epico") return "#B062FF";
        if (raridade == "Raro") return "#5DA0FF";
        return "#FFFFFF";
    }

    private void DesabilitarBotoes()
    {
        if (botaoPull != null) botaoPull.interactable = false;
        if (botaoPull10 != null) botaoPull10.interactable = false;
        if (botaoReset != null) botaoReset.interactable = false;
        if (botaoGemas != null) botaoGemas.interactable = false;
        if (botaoAbrirEvolucao != null) botaoAbrirEvolucao.interactable = false;
        if (botaoTrocarBanner != null) botaoTrocarBanner.interactable = false;
    }

    private void HabilitarBotoes()
    {
        if (botaoPull != null) botaoPull.interactable = true;
        if (botaoPull10 != null) botaoPull10.interactable = true;
        if (botaoReset != null) botaoReset.interactable = true;
        if (botaoGemas != null) botaoGemas.interactable = true;
        if (botaoAbrirEvolucao != null) botaoAbrirEvolucao.interactable = true;
        if (botaoTrocarBanner != null) botaoTrocarBanner.interactable = true;
    }

    public void AdicionarGemas()
    {
        if (animandoPull) return;
        TocarSom(somClique);
        gemas += gemasPorRecarga;
        resultText.text = "+" + gemasPorRecarga + " gemas adicionadas!\n\nSaldo: " + gemas;
        Salvar();
        AtualizarPokedex();
    }

    public void Resetar()
    {
        if (animandoPull) return;
        TocarSom(somClique);
        ultimoPullFoiPity = false;
        gemas = gemasIniciais;
        bannerAtualIndex = 0;

        foreach (var item in itens)
        {
            obtido[item.nome] = false;
            nivel[item.nome] = 0;
            fragmentos[item.nome] = 0;
        }

        foreach (var banner in banners)
        {
            totalPullsPorBanner[banner.nome] = 0;
            pullsSemRaridadePorBanner[banner.nome] = 0;
        }

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        resultText.text = "Progresso resetado!\nClique em PULL para comecar de novo.";

        AtualizarPokedex();
        AtualizarLinhasEvolucao();
        Debug.Log("Progresso resetado.");
    }

    private void Salvar()
    {
        PlayerPrefs.SetInt("gemas", gemas);
        PlayerPrefs.SetInt("bannerAtualIndex", bannerAtualIndex);

        foreach (var item in itens)
        {
            PlayerPrefs.SetInt("obtido_" + item.nome, obtido[item.nome] ? 1 : 0);
            PlayerPrefs.SetInt("nivel_" + item.nome, nivel[item.nome]);
            PlayerPrefs.SetInt("frag_" + item.nome, fragmentos[item.nome]);
        }

        foreach (var banner in banners)
        {
            PlayerPrefs.SetInt("pulls_" + banner.nome, totalPullsPorBanner[banner.nome]);
            PlayerPrefs.SetInt("pity_" + banner.nome, pullsSemRaridadePorBanner[banner.nome]);
        }

        PlayerPrefs.Save();
    }

    private void Carregar()
    {
        if (!PlayerPrefs.HasKey("gemas"))
        {
            Debug.Log("Nenhum save encontrado. Comecando do zero com " + gemasIniciais + " gemas.");
            return;
        }

        gemas = PlayerPrefs.GetInt("gemas", gemasIniciais);
        bannerAtualIndex = PlayerPrefs.GetInt("bannerAtualIndex", 0);

        foreach (var item in itens)
        {
            obtido[item.nome] = PlayerPrefs.GetInt("obtido_" + item.nome, 0) == 1;
            nivel[item.nome] = PlayerPrefs.GetInt("nivel_" + item.nome, 0);
            fragmentos[item.nome] = PlayerPrefs.GetInt("frag_" + item.nome, 0);
        }

        foreach (var banner in banners)
        {
            totalPullsPorBanner[banner.nome] = PlayerPrefs.GetInt("pulls_" + banner.nome, 0);
            pullsSemRaridadePorBanner[banner.nome] = PlayerPrefs.GetInt("pity_" + banner.nome, 0);
        }

        Debug.Log("Save carregado: gemas " + gemas + ", banner index " + bannerAtualIndex);
    }

    private GachaItem ExecutarPullSemMarcar()
    {
        Banner b = BannerAtual();
        GachaItem itemSorteado;
        ultimoPullFoiPity = false;

        int pityAtual = pullsSemRaridadePorBanner[b.nome];

        if (pityAtual >= b.pityLimite - 1)
        {
            itemSorteado = SortearItemPorRaridade(b.raridadeGarantida);
            ultimoPullFoiPity = true;
        }
        else
        {
            itemSorteado = SortearItem();
        }

        return itemSorteado;
    }

    private void AplicarPullNoInventario(GachaItem item)
    {
        Banner b = BannerAtual();
        totalPullsPorBanner[b.nome]++;

        if (!obtido[item.nome])
        {
            obtido[item.nome] = true;
        }
        else
        {
            if (nivel[item.nome] < nivelMaximo)
            {
                fragmentos[item.nome]++;
            }
        }

        if (item.raridade == b.raridadeGarantida)
        {
            pullsSemRaridadePorBanner[b.nome] = 0;
        }
        else
        {
            pullsSemRaridadePorBanner[b.nome]++;
        }

        int totalAtual = totalPullsPorBanner[b.nome];
        int pityAtual = pullsSemRaridadePorBanner[b.nome];

        Debug.Log("Pull " + totalAtual + " (" + b.nome + "): " + item.nome + " - " + item.raridade + (ultimoPullFoiPity ? " [PITY]" : "") + " | Pity: " + pityAtual);
    }

    // Sorteia entre itens do banner atual (respeitando pesos)
    private GachaItem SortearItem()
    {
        List<GachaItem> pool = ItensDoBannerAtual();

        if (pool.Count == 0) pool = itens;

        float pesoTotal = 0f;
        foreach (var item in pool)
            pesoTotal += item.peso;

        float sorteio = Random.Range(0f, pesoTotal);

        float acumulado = 0f;
        foreach (var item in pool)
        {
            acumulado += item.peso;
            if (sorteio <= acumulado)
                return item;
        }

        return pool[0];
    }

    // Sorteia entre itens da raridade alvo DENTRO do banner atual
    private GachaItem SortearItemPorRaridade(string raridade)
    {
        List<GachaItem> pool = ItensDoBannerAtual();
        List<GachaItem> filtrados = new List<GachaItem>();

        foreach (var item in pool)
        {
            if (item.raridade == raridade)
                filtrados.Add(item);
        }

        if (filtrados.Count == 0)
            return SortearItem();

        float pesoTotal = 0f;
        foreach (var item in filtrados)
            pesoTotal += item.peso;

        float sorteio = Random.Range(0f, pesoTotal);
        float acumulado = 0f;
        foreach (var item in filtrados)
        {
            acumulado += item.peso;
            if (sorteio <= acumulado)
                return item;
        }

        return filtrados[0];
    }

    private void AtualizarPokedex()
    {
        StringBuilder sb = new StringBuilder();

        Banner b = BannerAtual();
        string nomeBanner = (b != null) ? b.nome : "???";
        int pityAtual = (b != null && pullsSemRaridadePorBanner.ContainsKey(b.nome)) ? pullsSemRaridadePorBanner[b.nome] : 0;
        int pityLim = (b != null) ? b.pityLimite : 0;
        int totalAtual = (b != null && totalPullsPorBanner.ContainsKey(b.nome)) ? totalPullsPorBanner[b.nome] : 0;

        sb.AppendLine("GEMAS: " + gemas);
        sb.AppendLine("Banner: " + nomeBanner);
        sb.AppendLine("");
        sb.AppendLine("=== POKEDEX ===");
        sb.AppendLine("");

        int descobertos = 0;

        foreach (var item in itens)
        {
            string corLinha = CorPorRaridade(item.raridade);

            if (obtido[item.nome])
            {
                int n = nivel[item.nome];
                int f = fragmentos[item.nome];

                string estrelas = "";
                for (int i = 0; i <= n; i++) estrelas += "*";

                string statusFrag;
                if (n >= nivelMaximo)
                {
                    statusFrag = "MAX";
                }
                else
                {
                    int custo = custoPorNivel[n];
                    statusFrag = "frag: " + f + "/" + custo;
                }

                sb.AppendLine("<color=" + corLinha + ">" + item.nome + "</color> " + estrelas + " (" + statusFrag + ")");
                descobertos++;
            }
            else
            {
                sb.AppendLine("??? (" + item.raridade + ")");
            }
        }

        sb.AppendLine("");
        sb.AppendLine("Descobertos: " + descobertos + "/" + itens.Count);
        sb.AppendLine("Pity: " + pityAtual + "/" + pityLim);
        sb.AppendLine("Pulls neste banner: " + totalAtual);

        pokedexText.text = sb.ToString();
    }

    // Fecha o painel de revelacao (chamado pelo botao do painel)
    public void FecharRevelacao()
    {   
        TocarSom(somClique);
        if (painelRevelacao != null) painelRevelacao.SetActive(false);
    }

    private void TocarSom(AudioClip clip)
    {
        if (audioSourceEfeitos != null && clip != null)
        {
            audioSourceEfeitos.PlayOneShot(clip);
        }
    }

    // Animacao de zoom in na imagem revelada
    private IEnumerator AnimarZoomImagem(float duracao = 1f)
    {
        if (imagemRevelacao == null) yield break;

        Transform t = imagemRevelacao.transform;

        // Comeca pequeno
        t.localScale = Vector3.one * 0.1f;

        float tempoDecorrido = 0f;

        while (tempoDecorrido < duracao)
        {
            tempoDecorrido += Time.deltaTime;
            float progresso = tempoDecorrido / duracao;

            // Efeito "bouncy" - cresce passando do alvo e volta
            // Usa uma curva ease-out elastic simples
            float escala = EaseOutBack(progresso);

            t.localScale = Vector3.one * escala;
            yield return null;
        }

        // Garante que terminou no tamanho exato 1.0
        t.localScale = Vector3.one;
    }

    // Funcao de easing "bouncy" - cresce, ultrapassa, e volta
    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // Gera particulas douradas ao redor da imagem revelada
    private void GerarParticulasLendario()
    {
        if (particulaBrilhoTemplate == null) return;

        int quantidade = 20; // numero de particulas

        for (int i = 0; i < quantidade; i++)
        {
            // Instancia uma copia do template como filho do mesmo pai
            GameObject p = Instantiate(particulaBrilhoTemplate, particulaBrilhoTemplate.transform.parent);
            p.SetActive(true);

            // Posicao aleatoria ao redor do centro
            RectTransform rt = p.GetComponent<RectTransform>();
            float x = Random.Range(-350f, 350f);
            float y = Random.Range(-300f, 300f);
            rt.anchoredPosition = new Vector2(x, y);

            // Tamanho aleatorio (entre 40 e 80)
            float tamanho = Random.Range(40f, 80f);
            rt.sizeDelta = new Vector2(tamanho, tamanho);

            // Cor dourada com pouco de variacao
            UnityEngine.UI.Image img = p.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                float r = Random.Range(0.9f, 1f);
                float g = Random.Range(0.7f, 0.95f);
                float b = Random.Range(0.1f, 0.4f);
                img.color = new Color(r, g, b, 1f);
            }

            // Inicia coroutine de fade out + destruicao
            StartCoroutine(AnimarParticula(p, Random.Range(0.8f, 1.6f)));
        }
    }

    // Anima uma particula individual: fade out + escala crescendo + destruir no fim
    private IEnumerator AnimarParticula(GameObject particula, float duracao)
    {
        if (particula == null) yield break;

        UnityEngine.UI.Image img = particula.GetComponent<UnityEngine.UI.Image>();
        RectTransform rt = particula.GetComponent<RectTransform>();

        Vector3 escalaInicial = rt.localScale;
        Vector3 escalaFinal = escalaInicial * 1.8f;
        Color corInicial = img != null ? img.color : Color.white;

        float tempoDecorrido = 0f;

        while (tempoDecorrido < duracao)
        {
            tempoDecorrido += Time.deltaTime;
            float progresso = tempoDecorrido / duracao;

            // Escala cresce
            rt.localScale = Vector3.Lerp(escalaInicial, escalaFinal, progresso);

            // Alpha diminui (fade out)
            if (img != null)
            {
                Color c = corInicial;
                c.a = 1f - progresso;
                img.color = c;
            }

            yield return null;
        }

        // Remove a particula da cena
        Destroy(particula);
    }

    // Aplica cor de fundo no painel de revelacao conforme a raridade
    private void ColorirFundoPorRaridade(string raridade)
    {
        if (fundoPainelRevelacao == null) return;

        // Para qualquer pulse anterior (caso esteja rodando)
        StopCoroutine("PulsarFundoLendario");

        Color cor;

        if (raridade == "Lendario")
        {
            // Inicia pulse dourado
            cor = new Color(0.5f, 0.4f, 0.1f, 0.95f);
            fundoPainelRevelacao.color = cor;
            StartCoroutine("PulsarFundoLendario");
        }
        else if (raridade == "Epico")
        {
            cor = new Color(0.3f, 0.15f, 0.4f, 0.95f); // roxo escuro
            fundoPainelRevelacao.color = cor;
        }
        else if (raridade == "Raro")
        {
            cor = new Color(0.1f, 0.2f, 0.4f, 0.95f); // azul escuro
            fundoPainelRevelacao.color = cor;
        }
        else
        {
            cor = new Color(0.15f, 0.15f, 0.2f, 0.95f); // cinza escuro (padrao)
            fundoPainelRevelacao.color = cor;
        }
    }

    // Coroutine que pulsa a cor dourada do fundo enquanto o painel estiver aberto
    private IEnumerator PulsarFundoLendario()
    {
        if (fundoPainelRevelacao == null) yield break;

        Color corMin = new Color(0.4f, 0.3f, 0.05f, 0.95f);  // dourado escuro
        Color corMax = new Color(0.7f, 0.55f, 0.15f, 0.95f); // dourado mais claro

        float tempo = 0f;

        // Continua pulsando enquanto o painel estiver ativo
        while (painelRevelacao != null && painelRevelacao.activeSelf)
        {
            tempo += Time.deltaTime;

            // PingPong faz o valor ir e voltar entre 0 e 1
            float t = Mathf.PingPong(tempo * 1.5f, 1f);

            fundoPainelRevelacao.color = Color.Lerp(corMin, corMax, t);
            yield return null;
        }
    }
}