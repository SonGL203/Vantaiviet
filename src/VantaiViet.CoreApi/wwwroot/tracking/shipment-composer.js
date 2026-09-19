export function initializeShipmentComposer(map,api){
 const $=id=>document.getElementById(id),pins=L.layerGroup().addTo(map);
 let stops={},draft=null,generation=0;
 function reset(){generation++;stops={};draft=null;pins.clearLayers();$("shipmentForm").reset();$("createShipment").disabled=false;$("shipmentCoordinates").textContent="Chưa chọn hai điểm.";$("shipmentMessage").textContent="";$("publishShipment").hidden=true;$("publishShipment").disabled=false;$("newShipment").hidden=true;}
 function draw(){pins.clearLayers();for(const [key,p] of Object.entries(stops)){L.marker([p.latitude,p.longitude]).addTo(pins).bindTooltip(key==="pickup"?"Tin mới · lấy hàng":"Tin mới · giao hàng");}
  $("shipmentCoordinates").textContent=["pickup","delivery"].map(key=>(key==="pickup"?"Lấy: ":"Giao: ")+(stops[key]?stops[key].latitude.toFixed(6)+", "+stops[key].longitude.toFixed(6):"chưa chọn")).join(" · ");}
 $("shipmentPoint").onchange=()=>{if($("shipmentPoint").value)$("clickTarget").value="";};
 $("showShipmentMap").onclick=()=>document.getElementById("map").scrollIntoView({behavior:"smooth",block:"center"});
 map.on("click",e=>{const target=$("shipmentPoint").value;if(!target||draft||$("session").hidden)return;stops[target]={latitude:e.latlng.lat,longitude:e.latlng.lng};draw();});
 $("shipmentForm").onsubmit=async e=>{
  e.preventDefault();if(draft)return;
  if(!stops.pickup||!stops.delivery){$("shipmentMessage").textContent="Chọn đủ điểm lấy và giao hàng trên bản đồ.";return;}
  const date=new Date($("shipmentPickupAt").value);
  if(!Number.isFinite(date.getTime())||date<=new Date()){ $("shipmentMessage").textContent="Giờ lấy hàng phải ở tương lai.";return; }
  const current=generation;$("createShipment").disabled=true;
  try{
   const result=await api("shipments","POST",{title:$("shipmentTitle").value,pickupAddress:$("shipmentPickupAddress").value,deliveryAddress:$("shipmentDeliveryAddress").value,weightKg:Number($("shipmentWeight").value),pickupAt:date.toISOString(),pickup:stops.pickup,delivery:stops.delivery});
   if(current!==generation)return;draft=result;$("shipmentPoint").value="";$("publishShipment").hidden=false;$("newShipment").hidden=false;
   $("shipmentMessage").textContent="Đã lưu nháp: "+result.id+". Chưa đăng công khai. Tin đã lưu không thay đổi khi bạn sửa form này.";
  }catch(e){if(current===generation){$("shipmentMessage").textContent="Chưa xác nhận lưu thành công: "+e.message+". Nếu mất mạng, kiểm tra danh sách tin của bạn trước khi gửi lại.";$("createShipment").disabled=false;}}
 };
 $("publishShipment").onclick=async()=>{
  if(!draft)return;const current=generation;$("publishShipment").disabled=true;
  try{const result=await api("shipments/"+draft.id+"/publish","POST",{version:draft.version});if(current!==generation)return;draft=result;$("publishShipment").hidden=true;$("shipmentMessage").textContent="Đã đăng tin: "+result.id+". Tài xế có thể gửi đề nghị nhận đơn.";}
  catch(e){if(current===generation){$("shipmentMessage").textContent="Không đăng được: "+e.message;$("publishShipment").disabled=false;}}
 };
 $("newShipment").onclick=reset;
 return {reset,stopPicking:()=>{$("shipmentPoint").value="";}};
}
