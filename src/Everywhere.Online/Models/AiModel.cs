namespace Everywhere.Online.Models;

[Serializable]
public partial class AiModel
{
    /// <summary>
    /// 模型名称，主要标记
    /// </summary>
    public required string Model { get; set; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public required string Display { get; set; }

    /// <summary>
    /// 供应商
    /// </summary>
    public required string Supplier { get; set; }
    
    /// <summary>
    /// 模型支持的最大Token数
    /// </summary>
    public required int MaxContextTokens { get; set; }
} 