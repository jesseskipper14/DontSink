//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;

//public class WaterStencilPassFeature : ScriptableRendererFeature
//{
//    [System.Serializable]
//    public class WaterStencilSettings
//    {
//        public LayerMask waterLayer;   // Layer containing your water objects
//        public Material stencilMaterial; // Material that writes to stencil
//        public int stencilRef = 2;     // Stencil reference value
//    }

//    public WaterStencilSettings settings = new WaterStencilSettings();

//    class WaterStencilPass : ScriptableRenderPass
//    {
//        public LayerMask layerMask;
//        public Material material;
//        public int stencilRef;
//        private FilteringSettings filteringSettings;
//        private ShaderTagId shaderTagId = new ShaderTagId("UniversalForward");

//        public WaterStencilPass(LayerMask layerMask, Material material, int stencilRef)
//        {
//            this.layerMask = layerMask;
//            this.material = material;
//            this.stencilRef = stencilRef;
//            filteringSettings = new FilteringSettings(RenderQueueRange.transparent, layerMask);
//        }

//        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
//        {
//            if (material == null) return;

//            CommandBuffer cmd = CommandBufferPool.Get("WaterStencilPass");

//            var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
//            var drawSettings = CreateDrawingSettings(shaderTagId, ref renderingData, sortFlags);
//            drawSettings.overrideMaterial = material;

//            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);

//            context.ExecuteCommandBuffer(cmd);
//            CommandBufferPool.Release(cmd);
//        }
//    }

//    WaterStencilPass stencilPass;

//    public override void Create()
//    {
//        stencilPass = new WaterStencilPass(settings.waterLayer, settings.stencilMaterial, settings.stencilRef);
//        stencilPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
//    }

//    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//    {
//        renderer.EnqueuePass(stencilPass);
//    }
//}
