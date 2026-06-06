using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 音频剪辑工具：可视化波形、裁剪区间、试听、导出 WAV。
/// 菜单入口：Tools/音频剪辑工具
/// </summary>
public class AudioClipTrimmer : EditorWindow
{
    private AudioClip _clip;
    private float _trimStart;
    private float _trimEnd;

    // 波形缓存（纹理，仅在 Clip 变更时重建）
    private Texture2D _waveformTexture;
    private float[] _waveformData;
    private float[] _cachedAllSamples;
    private int _cachedActualChannels;
    private const int WaveformSamples = 4000;

    // 播放控制
    private AudioSource _previewSource;
    private GameObject _previewGO;
    private bool _isPlaying;
    private double _playStartTime;
    private float _playStartOffset;
    private float _playDuration;

    private Vector2 _scrollPos;
    private string _exportPath = "";

    [MenuItem("Tools/音频剪辑工具")]
    public static void ShowWindow()
    {
        var window = GetWindow<AudioClipTrimmer>("音频剪辑工具");
        window.minSize = new Vector2(500, 420);
    }

    private void OnEnable()
    {
        Undo.undoRedoPerformed += Repaint;
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        StopAllPlayback();
        DestroyWaveformTexture();
        Undo.undoRedoPerformed -= Repaint;
        EditorApplication.update -= OnEditorUpdate;
    }

    /// <summary>
    /// 每帧更新：自动停止播放 + 播放中持续重绘。
    /// </summary>
    private void OnEditorUpdate()
    {
        if (_isPlaying && _previewSource != null)
        {
            // 到达裁剪结束点或音频自然结束 → 停止
            float elapsed = (float)(EditorApplication.timeSinceStartup - _playStartTime);
            if (!_previewSource.isPlaying || elapsed >= _playDuration)
            {
                StopAllPlayback();
            }
            Repaint();
        }
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawClipSelection();
        if (_clip != null)
        {
            EditorGUILayout.Space(4);
            DrawClipInfo();
            EditorGUILayout.Space(4);
            DrawTrimControls();
            EditorGUILayout.Space(4);
            DrawWaveform();
            EditorGUILayout.Space(4);
            DrawPlaybackButtons();
            EditorGUILayout.Space(4);
            DrawExportSection();
        }

        EditorGUILayout.EndScrollView();
    }

    #region UI 绘制

    private void DrawClipSelection()
    {
        EditorGUILayout.LabelField("音频选择", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _clip = (AudioClip)EditorGUILayout.ObjectField("AudioClip", _clip, typeof(AudioClip), false);
        if (EditorGUI.EndChangeCheck())
        {
            OnClipChanged();
        }
    }

    private void DrawClipInfo()
    {
        EditorGUILayout.LabelField("音频信息", EditorStyles.boldLabel);
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.FloatField("时长 (秒)", _clip.length);
        EditorGUILayout.IntField("采样率", _clip.frequency);
        EditorGUILayout.IntField("声道数", _clip.channels);
        EditorGUILayout.IntField("总采样数", _clip.samples);
        EditorGUI.EndDisabledGroup();
    }

    private void DrawTrimControls()
    {
        EditorGUILayout.LabelField("裁剪范围", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("起始", GUILayout.Width(30));
        _trimStart = EditorGUILayout.Slider(_trimStart, 0f, _trimEnd);
        EditorGUILayout.LabelField($"{_trimStart:F3}s", GUILayout.Width(55));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("结束", GUILayout.Width(30));
        _trimEnd = EditorGUILayout.Slider(_trimEnd, _trimStart, _clip.length);
        EditorGUILayout.LabelField($"{_trimEnd:F3}s", GUILayout.Width(55));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全选", GUILayout.Width(60)))
        {
            _trimStart = 0f;
            _trimEnd = _clip.length;
        }
        if (GUILayout.Button("重置", GUILayout.Width(60)))
        {
            _trimStart = 0f;
            _trimEnd = _clip.length;
        }
        float duration = _trimEnd - _trimStart;
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"裁剪时长: {duration:F3}s", GUILayout.Width(130));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawWaveform()
    {
        EditorGUILayout.LabelField("波形", EditorStyles.boldLabel);

        Rect waveformRect = GUILayoutUtility.GetRect(position.width - 20, 120);
        if (waveformRect.width < 10) return;

        // 背景
        EditorGUI.DrawRect(waveformRect, new Color(0.15f, 0.15f, 0.15f));

        if (_waveformTexture != null)
        {
            // 绘制缓存的波形纹理
            GUI.DrawTexture(waveformRect, _waveformTexture);
        }

        if (_waveformData == null || _waveformData.Length == 0) return;

        float totalDuration = _clip.length;
        float pixelsPerSecond = waveformRect.width / totalDuration;

        // 裁剪区间高亮
        float startX = waveformRect.x + _trimStart * pixelsPerSecond;
        float endX = waveformRect.x + _trimEnd * pixelsPerSecond;
        Rect trimRect = new Rect(startX, waveformRect.y, endX - startX, waveformRect.height);
        EditorGUI.DrawRect(trimRect, new Color(0.2f, 0.5f, 0.8f, 0.25f));

        // 裁剪边界线
        DrawTrimHandles(waveformRect, startX, endX);

        // 播放头
        if (_isPlaying && _previewSource != null)
        {
            float currentTime = _playStartOffset + (float)(EditorApplication.timeSinceStartup - _playStartTime);
            if (currentTime <= totalDuration)
            {
                float headX = waveformRect.x + currentTime * pixelsPerSecond;
                if (headX >= waveformRect.x && headX <= waveformRect.xMax)
                {
                    Handles.color = Color.red;
                    Handles.DrawLine(new Vector3(headX, waveformRect.y), new Vector3(headX, waveformRect.yMax));
                }
            }
        }

        // 时间刻度
        DrawTimeRuler(waveformRect, totalDuration);
    }

    private void DrawTrimHandles(Rect waveformRect, float startX, float endX)
    {
        Handles.color = new Color(0.2f, 0.8f, 1f);
        Handles.DrawLine(new Vector3(startX, waveformRect.y), new Vector3(startX, waveformRect.yMax));
        Handles.DrawLine(new Vector3(endX, waveformRect.y), new Vector3(endX, waveformRect.yMax));

        float handleSize = 8f;
        EditorGUI.DrawRect(new Rect(startX - handleSize / 2, waveformRect.y, handleSize, handleSize), Color.cyan);
        EditorGUI.DrawRect(new Rect(endX - handleSize / 2, waveformRect.yMax - handleSize, handleSize, handleSize), Color.cyan);
    }

    private void DrawTimeRuler(Rect rect, float totalDuration)
    {
        int tickCount = Mathf.Max(1, (int)(totalDuration / 0.5f));
        float pixelsPerSecond = rect.width / totalDuration;

        Handles.color = new Color(0.5f, 0.5f, 0.5f);
        var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter };

        for (int i = 0; i <= tickCount; i++)
        {
            float time = totalDuration * i / tickCount;
            float x = rect.x + time * pixelsPerSecond;
            Handles.DrawLine(new Vector3(x, rect.yMax - 4), new Vector3(x, rect.yMax));
            if (i % 2 == 0)
            {
                Rect labelRect = new Rect(x - 20, rect.yMax - 16, 40, 14);
                GUI.Label(labelRect, $"{time:F1}s", style);
            }
        }
    }

    private void DrawPlaybackButtons()
    {
        EditorGUILayout.LabelField("试听", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (_isPlaying)
        {
            if (GUILayout.Button("■ 停止", GUILayout.Height(28)))
                StopAllPlayback();
        }
        else
        {
            if (GUILayout.Button("▶ 播放原始", GUILayout.Height(28)))
                PlayClip(_clip, 0f, _clip.length);
            if (GUILayout.Button("▶ 播放裁剪", GUILayout.Height(28)))
                PlayClip(_clip, _trimStart, _trimEnd - _trimStart);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawExportSection()
    {
        EditorGUILayout.LabelField("导出", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("路径:", GUILayout.Width(30));
        _exportPath = EditorGUILayout.TextField(_exportPath);
        if (GUILayout.Button("浏览", GUILayout.Width(50)))
        {
            string dir = string.IsNullOrEmpty(_exportPath)
                ? Application.dataPath
                : Path.GetDirectoryName(_exportPath);
            string path = EditorUtility.SaveFilePanel("导出 WAV", dir, GetExportFileName(), "wav");
            if (!string.IsNullOrEmpty(path))
                _exportPath = path;
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("导出裁剪音频", GUILayout.Height(30)))
        {
            ExportTrimmedWav();
        }
    }

    #endregion

    #region 核心逻辑

    private void OnClipChanged()
    {
        StopAllPlayback();
        _trimStart = 0f;
        _trimEnd = _clip != null ? _clip.length : 0f;
        _cachedAllSamples = null;
        _cachedActualChannels = 0;
        RebuildWaveformData();
    }

    private void RebuildWaveformData()
    {
        DestroyWaveformTexture();
        if (_clip == null) { _waveformData = null; return; }

        _cachedAllSamples = ExtractAllSamples(_clip);
        if (_cachedAllSamples == null || _cachedAllSamples.Length == 0)
        {
            _waveformData = null;
            return;
        }

        // 反推实际声道数
        _cachedActualChannels = _clip.samples > 0
            ? _cachedAllSamples.Length / _clip.samples
            : _clip.channels;
        if (_cachedActualChannels <= 0) _cachedActualChannels = _clip.channels;

        int totalSamples = _cachedAllSamples.Length;
        int sampleStep = Mathf.Max(1, totalSamples / WaveformSamples);
        int outputLength = Mathf.Min(WaveformSamples, totalSamples / sampleStep);

        _waveformData = new float[outputLength];

        for (int i = 0; i < outputLength; i++)
        {
            int start = i * sampleStep;
            int end = Mathf.Min(start + sampleStep, totalSamples);
            float max = 0f;
            for (int j = start; j < end; j++)
            {
                float abs = Mathf.Abs(_cachedAllSamples[j]);
                if (abs > max) max = abs;
            }
            _waveformData[i] = max;
        }

        BuildWaveformTexture();
    }

    /// <summary>
    /// 将波形数据烘焙到纹理，避免每帧逐像素绘制。
    /// </summary>
    private void BuildWaveformTexture()
    {
        DestroyWaveformTexture();
        if (_waveformData == null || _waveformData.Length == 0) return;

        int width = Mathf.Min(_waveformData.Length, 2048);
        int height = 128;
        _waveformTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        _waveformTexture.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[width * height];
        // 透明背景
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, 0);

        int midY = height / 2;
        float step = (float)_waveformData.Length / width;

        for (int x = 0; x < width; x++)
        {
            int sampleIndex = Mathf.Clamp((int)(x * step), 0, _waveformData.Length - 1);
            int amplitude = Mathf.Clamp(Mathf.RoundToInt(_waveformData[sampleIndex] * midY * 0.9f), 0, midY);

            for (int y = 0; y <= amplitude; y++)
            {
                // 上半部分
                int idx = (midY + y) * width + x;
                if (idx >= 0 && idx < pixels.Length)
                    pixels[idx] = new Color32(100, 230, 100, 255);
                // 下半部分（镜像）
                idx = (midY - y) * width + x;
                if (idx >= 0 && idx < pixels.Length)
                    pixels[idx] = new Color32(100, 230, 100, 255);
            }
        }

        _waveformTexture.SetPixels32(pixels);
        _waveformTexture.Apply();
    }

    private void DestroyWaveformTexture()
    {
        if (_waveformTexture != null)
        {
            DestroyImmediate(_waveformTexture);
            _waveformTexture = null;
        }
    }

    private string GetExportFileName()
    {
        if (_clip == null) return "trimmed.wav";
        return $"{_clip.name}_trimmed_{_trimStart:F2}-{_trimEnd:F2}.wav";
    }

    private void ExportTrimmedWav()
    {
        if (_clip == null) return;
        if (string.IsNullOrEmpty(_exportPath))
        {
            _exportPath = EditorUtility.SaveFilePanel("导出 WAV",
                Application.dataPath, GetExportFileName(), "wav");
            if (string.IsNullOrEmpty(_exportPath)) return;
        }

        try
        {
            float[] samples = GetTrimmedSamples();
            if (samples == null || samples.Length == 0)
            {
                EditorUtility.DisplayDialog("导出失败",
                    "无法获取音频数据，请查看 Console 中的诊断日志。\n\n" +
                    "常见解决方法：\n" +
                    "1. 确认 Load Type = Decompress On Load\n" +
                    "2. 在 Inspector 中点击 Apply\n" +
                    "3. 右键音频文件 → Reimport",
                    "确定");
                return;
            }

            int channels = _cachedActualChannels > 0 ? _cachedActualChannels : _clip.channels;
            WriteWavFile(_exportPath, samples, channels, _clip.frequency);

            AssetDatabase.Refresh();
            Debug.Log($"[音频剪辑] 导出成功: {_exportPath}");
            EditorUtility.DisplayDialog("导出成功", $"已保存到:\n{_exportPath}", "确定");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[音频剪辑] 导出失败: {ex.Message}");
            EditorUtility.DisplayDialog("导出失败", ex.Message, "确定");
        }
    }

    /// <summary>
    /// 获取裁剪区间的采样数据。
    /// 优先从磁盘直接读取 WAV 文件（绕过 GetData），回退到 GetData。
    /// </summary>
    private float[] GetTrimmedSamples()
    {
        // 使用缓存的采样数据（RebuildWaveformData 中已加载）
        if (_cachedAllSamples == null || _cachedAllSamples.Length == 0)
        {
            Debug.LogError("[音频剪辑] 无法获取音频采样数据。");
            return null;
        }

        int channels = _cachedActualChannels > 0 ? _cachedActualChannels : _clip.channels;
        int sampleRate = _clip.frequency;
        int startSample = Mathf.FloorToInt(_trimStart * sampleRate) * channels;
        int endSample = Mathf.FloorToInt(_trimEnd * sampleRate) * channels;
        startSample = Mathf.Clamp(startSample, 0, _cachedAllSamples.Length);
        endSample = Mathf.Clamp(endSample, 0, _cachedAllSamples.Length);
        int trimLength = endSample - startSample;

        if (trimLength <= 0) return null;

        float[] trimmed = new float[trimLength];
        Array.Copy(_cachedAllSamples, startSample, trimmed, 0, trimLength);
        return trimmed;
    }

    /// <summary>
    /// 提取 AudioClip 的全部采样数据（交错格式）。
    /// 优先从磁盘直接读取 WAV 文件，回退到 AudioClip.GetData。
    /// </summary>
    private static float[] ExtractAllSamples(AudioClip clip)
    {
        // 1. 尝试从磁盘直接读取 WAV 文件
        string assetPath = AssetDatabase.GetAssetPath(clip);
        if (!string.IsNullOrEmpty(assetPath))
        {
            string fullPath = Path.GetFullPath(assetPath);
            if (File.Exists(fullPath))
            {
                string ext = Path.GetExtension(fullPath).ToLowerInvariant();
                if (ext == ".wav")
                {
                    var wavData = ReadWavPcmFloats(fullPath);
                    if (wavData != null && wavData.Length > 0)
                    {
                        Debug.Log($"[音频剪辑] 从磁盘读取 WAV 成功: {wavData.Length} samples");
                        return wavData;
                    }
                    Debug.LogWarning("[音频剪辑] WAV 磁盘读取失败，回退到 GetData");
                }
            }
        }

        // 2. 回退：使用 AudioClip.GetData
        if (clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
            double deadline = EditorApplication.timeSinceStartup + 2.0;
            while (clip.loadState == AudioDataLoadState.Loading
                   && EditorApplication.timeSinceStartup < deadline)
            {
                System.Threading.Thread.Sleep(10);
            }
        }

        int totalSamples = clip.samples * clip.channels;
        float[] samples = new float[totalSamples];
        bool ok = clip.GetData(samples, 0);

        if (!ok)
        {
            Debug.LogError("[音频剪辑] GetData 返回 false。");
            return null;
        }

        float maxAbs = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float abs = Mathf.Abs(samples[i]);
            if (abs > maxAbs) maxAbs = abs;
        }
        Debug.Log($"[音频剪辑] GetData: samples={samples.Length}, maxAbs={maxAbs:F6}, loadState={clip.loadState}");

        if (maxAbs < 0.000001f)
        {
            Debug.LogError("[音频剪辑] GetData 返回全零。请确认:\n" +
                           "1. Load Type = Decompress On Load\n" +
                           "2. Inspector 中点击 Apply\n" +
                           "3. 右键音频文件 → Reimport");
            return null;
        }

        return samples;
    }

    /// <summary>
    /// 从 WAV 文件直接读取 PCM 数据并转为 float [-1,1]。
    /// 支持 8-bit / 16-bit / 24-bit / 32-bit PCM。
    /// </summary>
    private static float[] ReadWavPcmFloats(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);

            // ── RIFF header ──
            if (reader.ReadInt32() != 0x46464952) return null; // "RIFF"
            reader.ReadInt32(); // file size
            if (reader.ReadInt32() != 0x45564157) return null; // "WAVE"

            int channels = 0;
            int sampleRate = 0;
            int bitsPerSample = 0;
            byte[] pcmRaw = null;

            // ── 读取 chunks ──
            while (stream.Position < stream.Length)
            {
                int chunkId = reader.ReadInt32();
                int chunkSize = reader.ReadInt32();

                if (chunkId == 0x20746D66) // "fmt "
                {
                    int format = reader.ReadInt16(); // 1 = PCM
                    if (format != 1)
                    {
                        Debug.LogWarning($"[音频剪辑] WAV 格式非 PCM (format={format})，跳过磁盘读取。");
                        return null;
                    }
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32(); // byte rate
                    reader.ReadInt16(); // block align
                    bitsPerSample = reader.ReadInt16();
                    // 跳过 fmt chunk 可能的额外字节
                    int consumed = 16;
                    if (chunkSize > consumed)
                        stream.Seek(chunkSize - consumed, SeekOrigin.Current);
                }
                else if (chunkId == 0x61746164) // "data"
                {
                    pcmRaw = reader.ReadBytes(chunkSize);
                }
                else
                {
                    // 跳过未知 chunk
                    stream.Seek(chunkSize, SeekOrigin.Current);
                }
            }

            if (pcmRaw == null || channels == 0 || bitsPerSample == 0)
            {
                Debug.LogWarning("[音频剪辑] WAV 文件解析不完整。");
                return null;
            }

            int bytesPerSample = bitsPerSample / 8;
            int totalPcmSamples = pcmRaw.Length / bytesPerSample;
            float[] result = new float[totalPcmSamples];

            for (int i = 0; i < totalPcmSamples; i++)
            {
                int offset = i * bytesPerSample;
                if (offset + bytesPerSample > pcmRaw.Length) break;

                switch (bitsPerSample)
                {
                    case 8:
                        result[i] = (pcmRaw[offset] - 128f) / 128f;
                        break;
                    case 16:
                        short s16 = (short)(pcmRaw[offset] | (pcmRaw[offset + 1] << 8));
                        result[i] = s16 / 32768f;
                        break;
                    case 24:
                        int s24 = pcmRaw[offset] | (pcmRaw[offset + 1] << 8) | (pcmRaw[offset + 2] << 16);
                        if ((s24 & 0x800000) != 0) s24 |= unchecked((int)0xFF000000); // 符号扩展
                        result[i] = s24 / 8388608f;
                        break;
                    case 32:
                        int s32 = pcmRaw[offset] | (pcmRaw[offset + 1] << 8)
                                | (pcmRaw[offset + 2] << 16) | (pcmRaw[offset + 3] << 24);
                        result[i] = s32 / 2147483648f;
                        break;
                }
            }

            Debug.Log($"[音频剪辑] WAV 解析: {channels}ch, {sampleRate}Hz, {bitsPerSample}bit, {totalPcmSamples} samples");
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[音频剪辑] WAV 读取异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 写入标准 WAV 文件（PCM 16-bit, Little-Endian）。
    /// </summary>
    private static void WriteWavFile(string path, float[] samples, int channels, int sampleRate)
    {
        int sampleCount = samples.Length;
        int bitsPerSample = 16;
        int bytesPerSample = bitsPerSample / 8;
        int dataSize = sampleCount * bytesPerSample;

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        // ── RIFF Header (12 bytes) ──
        writer.Write(new byte[] { 0x52, 0x49, 0x46, 0x46 }); // "RIFF"
        writer.Write((uint)(36 + dataSize));                   // file size - 8
        writer.Write(new byte[] { 0x57, 0x41, 0x56, 0x45 }); // "WAVE"

        // ── fmt chunk (24 bytes) ──
        writer.Write(new byte[] { 0x66, 0x6D, 0x74, 0x20 }); // "fmt "
        writer.Write((uint)16);                                // chunk data size
        writer.Write((ushort)1);                               // PCM format
        writer.Write((ushort)channels);
        writer.Write((uint)sampleRate);
        writer.Write((uint)(sampleRate * channels * bytesPerSample)); // byte rate
        writer.Write((ushort)(channels * bytesPerSample));            // block align
        writer.Write((ushort)bitsPerSample);

        // ── data chunk ──
        writer.Write(new byte[] { 0x64, 0x61, 0x74, 0x61 }); // "data"
        writer.Write((uint)dataSize);

        // PCM samples: float [-1, 1] → int16, little-endian
        for (int i = 0; i < sampleCount; i++)
        {
            float clamped = Mathf.Clamp(samples[i], -1f, 1f);
            short pcm = (short)(clamped * 32767f);
            // 手动写入小端字节序，确保跨平台正确
            writer.Write((byte)(pcm & 0xFF));
            writer.Write((byte)((pcm >> 8) & 0xFF));
        }
    }

    #endregion

    #region 播放控制

    /// <summary>
    /// 直接播放原始 AudioClip 的指定区间。
    /// 通过 AudioSource.time 设置起始位置，由 OnEditorUpdate 自动停止。
    /// </summary>
    private void PlayClip(AudioClip clip, float startTime, float duration)
    {
        StopAllPlayback();

        if (clip == null || duration <= 0f) return;

        // 创建临时 GameObject + AudioSource
        _previewGO = new GameObject("AudioTrimmerPreview");
        _previewGO.hideFlags = HideFlags.HideAndDontSave;
        _previewSource = _previewGO.AddComponent<AudioSource>();
        _previewSource.playOnAwake = false;
        _previewSource.clip = clip;
        _previewSource.time = Mathf.Clamp(startTime, 0f, clip.length);
        _previewSource.Play();

        _isPlaying = true;
        _playStartTime = EditorApplication.timeSinceStartup;
        _playStartOffset = startTime;
        _playDuration = duration;
    }

    private void StopAllPlayback()
    {
        if (_previewSource != null)
        {
            _previewSource.Stop();
        }
        if (_previewGO != null)
        {
            DestroyImmediate(_previewGO);
        }
        _previewSource = null;
        _previewGO = null;
        _isPlaying = false;
    }

    #endregion
}
