// This public configuration may contain browser-scoped keys only, never server secrets.
export function initializeBasemap(map) {
 let layer=null,generation=0;
 const status=document.getElementById("mapStatus"),retry=document.getElementById("retryMap");
 async function load(){
  const current=++generation;
  retry.disabled=true;status.textContent="Đang tải nền bản đồ…";
  if(layer){map.removeLayer(layer);layer=null;}
  try{
   const response=await fetch("/tracking/map-config.json",{cache:"no-cache"});
   if(!response.ok)throw new Error("config");
   const config=await response.json();
   if(current!==generation)return;
   if(!["vector","raster"].includes(config.type)||!config.url?.startsWith("https://")||!Number.isInteger(config.maxZoom)||config.maxZoom<1||config.maxZoom>22)throw new Error("config");
   const failed=()=>{if(current===generation)status.textContent="Không tải đủ nền bản đồ. Bấm Thử lại; GPS vẫn hoạt động.";};
   const ready=()=>{if(current===generation)status.textContent="Nền bản đồ · "+config.name;};
   if(config.type==="vector"){
    if(!L.maplibreGL)throw new Error("library");
    layer=L.maplibreGL({style:config.url,attribution:config.attribution,interactive:false}).addTo(map);
    const renderer=layer.getMaplibreMap();
    renderer.on("error",failed);
    renderer.once("idle",ready);
   }else{
    let errors=false;
    layer=L.tileLayer(config.url,{maxZoom:config.maxZoom,attribution:config.attribution});
    layer.on("loading",()=>{errors=false;});
    layer.on("tileerror",()=>{errors=true;failed();});
    layer.on("load",()=>{if(!errors)ready();});
    layer.addTo(map);
   }
   map.setMaxZoom(config.maxZoom);
  }catch{if(current===generation)status.textContent="Không tải được nền bản đồ hoặc cấu hình. Bấm Thử lại; GPS vẫn hoạt động.";}
  finally{if(current===generation)retry.disabled=false;}
 }
 retry.addEventListener("click",()=>void load());
 void load();
}
