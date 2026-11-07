using Godot;
using San9.Rendering;
using San9.Application.Presentation;

namespace San9.Client.Godot;

public partial class MapSceneView
{
    private readonly Dictionary<int, PersonnelAdviceGrade> _personnelRecommendations = [];
    private long _drawnPersonnelRecommendationRevision = -1;
    private ShaderMaterial _personnelFavorableMaterial = null!;
    private ShaderMaterial _personnelFavorableHoverMaterial = null!;
    private ShaderMaterial _personnelSpecialMaterial = null!;
    private ShaderMaterial _personnelSpecialHoverMaterial = null!;
    private ShaderMaterial _personnelHoverMaterial = null!;

    private void PreparePersonnelOverlayMaterials(Shader shader)
    {
        ShaderMaterial Material(Vector3 rgb, float weight)
        {
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("overlay_rgb",rgb);
            material.SetShaderParameter("weight",weight);
            return material;
        }
        // Use the existing region overlay's RGB555 blend; hover replaces the base tint.
        _personnelFavorableMaterial = Material(new(48,168,248),64);
        _personnelFavorableHoverMaterial = Material(new(112,216,255),96);
        _personnelSpecialMaterial = Material(new(248,168,16),64);
        _personnelSpecialHoverMaterial = Material(new(255,224,96),96);
        _personnelHoverMaterial = Material(new(208,208,208),64);
    }

    private void RefreshPersonnelRecommendations()
    {
        long revision = PersonnelTargets?.RecommendationRevision ?? -1;
        if (_drawnPersonnelRecommendationRevision == revision) return;
        _drawnPersonnelRecommendationRevision = revision;
        _personnelRecommendations.Clear();
        if (PersonnelTargets is not { Kind: "search", IsCurrent: true } draft) return;
        foreach (int place in _personnelRegionMeshes.Keys)
            _personnelRecommendations.Add(place,draft.TargetAdviceGrade(place));
    }

    private void DrawPersonnelRecommendations(Action<Texture2D, Vector2, Material?, double> add)
    {
        if (PersonnelTargets is not { Kind: "search", IsCurrent: true }) return;
        foreach (var (place,priority) in _personnelRecommendations)
        {
            bool hovered = PersonnelHoverPlace == place;
            if (priority is not (PersonnelAdviceGrade.Favorable or PersonnelAdviceGrade.Special) && !hovered) continue;
            var material = priority switch
            {
                PersonnelAdviceGrade.Special => hovered ? _personnelSpecialHoverMaterial : _personnelSpecialMaterial,
                PersonnelAdviceGrade.Favorable => hovered ? _personnelFavorableHoverMaterial : _personnelFavorableMaterial,
                _ => _personnelHoverMaterial
            };
            foreach (var mesh in _personnelRegionMeshes[place])
                add(_meshTextures[mesh.ElevationMask],new(mesh.X,mesh.Y),material,1);
        }
    }

    private void DisposePersonnelOverlayMaterials()
    {
        // The common overlay shader is owned by _meshMaterials.
        _personnelFavorableMaterial.Dispose();
        _personnelFavorableHoverMaterial.Dispose();
        _personnelSpecialMaterial.Dispose();
        _personnelSpecialHoverMaterial.Dispose();
        _personnelHoverMaterial.Dispose();
    }
}
