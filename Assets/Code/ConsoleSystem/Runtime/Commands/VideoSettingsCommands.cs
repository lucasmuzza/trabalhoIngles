using UnityEngine;

namespace UnityConsole
{
    [ConsoleContainer("video")] // Agrupa os comandos gerando o prefixo "video."
    public class VideoSettingsCommands : MonoBehaviour
    {
        private void Awake()
        {
            // Registra as sugestões do comando de forma totalmente desacoplada
            if (ConsoleCommandManager.Instance != null)
            {
                ConsoleCommandManager.Instance.RegisterCommandSuggestions("video.resolution", new ResolutionSuggestionProvider());
            }
        }

        // Executável via console por: video.quality 2
        [ConsoleCommand("quality", "Altera o nível de qualidade gráfica geral do jogo.")]
        public void SetQuality(int level)
        {
            QualitySettings.SetQualityLevel(level, true);
            Debug.Log($"Qualidade gráfica alterada para: {QualitySettings.names[level]}");
        }

        // Executável via console por: video.resolution 1920 1080
        [ConsoleCommand("resolution", "Altera a resolução da janela para os valores passados ex: 1920 x 1080.")]
        public void SetResolution(int width, int height)
        {
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            Debug.Log($"Resolução alterada para: {width}x{height}");
        }
    }
}