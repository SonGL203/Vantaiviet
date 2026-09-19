import {initializeBasemap} from "./basemap.js";
import {initializeShipmentComposer} from "./shipment-composer.js";
const $=id=>document.getElementById(id);
let token="",userId="",tripId="",epoch=0,cursor=0,page=1,watch=null,hub=null,poll=null,points=new Map(),lastSent=0,busy=false,flushing=false,snapshot=null;
// Keep vector background and Leaflet GPS overlays aligned during large zoom changes.
const map=L.map("map",{zoomAnimation:false}).setView([16.1,106.2],6);
initializeBasemap(map);
const shipmentComposer=initializeShipmentComposer(map,api);
const actual=L.polyline([],{color:"#167bbb",weight:4}).addTo(map),planned=L.polyline([],{color:"#dd8b23",weight:4,dashArray:"8 8"}).addTo(map);
const markers=L.layerGroup().addTo(map);
const historyMarker=L.layerGroup().addTo(map);
let selectedHistoryId=null;
function historyPoints(){return [...points.values()].sort((a,b)=>new Date(a.recordedAt)-new Date(b.recordedAt)||a.id-b.id);}
function updateHistory(center=false){
 const list=historyPoints(),slider=$("historyPosition");slider.disabled=!list.length;slider.max=Math.max(0,list.length-1);
 historyMarker.clearLayers();
 let index=list.findIndex(p=>p.id===selectedHistoryId);
 if(index<0){selectedHistoryId=null;index=Math.max(0,list.length-1);}
 slider.value=index;
 if(!list.length){$("historyDetail").textContent="Chưa có lịch sử.";return;}
 const p=list[index];
 $("historyDetail").textContent=(selectedHistoryId===null?"Mới nhất trong lịch sử đã tải · ":"Đang xem lại · ")+(index+1)+"/"+list.length+" · "+new Date(p.recordedAt).toLocaleString("vi-VN")+" · "+p.latitude.toFixed(6)+", "+p.longitude.toFixed(6)+(p.simulated?" · GIẢ LẬP":"");
 if(selectedHistoryId!==null)L.circleMarker([p.latitude,p.longitude],{radius:12,color:"#8142a0",fillOpacity:.7}).addTo(historyMarker).bindTooltip("Điểm lịch sử");
 if(center)map.setView([p.latitude,p.longitude],Math.max(map.getZoom(),15));
}
async function endSession(expired=false){
 stopGps();shipmentComposer.reset();epoch++;busy=false;if(poll)clearInterval(poll);poll=null;
 const previousHub=hub;hub=null;token="";tripId="";userId="";erase();
 $("session").hidden=true;$("login").hidden=false;$("trips").length=1;page=1;$("realtime").textContent="Chưa kết nối";
 message(expired?"Phiên đăng nhập đã hết hạn. Đăng nhập lại để tiếp tục; điểm GPS chưa gửi vẫn được giữ trên thiết bị.":"Đã đăng xuất. Vị trí chưa gửi vẫn được giữ riêng theo tài khoản trên thiết bị.");
 if(previousHub)await previousHub.stop();
}
function message(text){$("message").textContent=text;}
async function api(path,method="GET",body){
 const response=await fetch("/api/"+path,{method,headers:{Authorization:"Bearer "+token,...(body?{"Content-Type":"application/json"}:{})},body:body?JSON.stringify(body):undefined});
 if(!response.ok){let p;try{p=await response.json();}catch{}if(response.status===401)await endSession(true);const e=new Error(response.status===401?"Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.":p?.errorCode||("HTTP "+response.status));e.status=response.status;throw e;}
 return (await response.json()).data;
}
function stopGps(){if(watch!==null){navigator.geolocation.clearWatch(watch);watch=null;} $("startGps").disabled=false;}
function queueKey(){return "vtt-gps:"+userId+":"+tripId;}
function readQueue(){try{return JSON.parse(localStorage.getItem(queueKey())||"[]");}catch{return [];}}
function saveQueue(q){localStorage.setItem(queueKey(),JSON.stringify(q));$("queue").textContent=q.length?("Đang chờ gửi: "+q.length+" điểm"):"Đã gửi hết vị trí.";}
async function flush(){
 if(flushing||!tripId||!snapshot?.canSend)return;
const generation=epoch,batch=readQueue().slice(0,100);if(!batch.length)return;
 flushing=true;try{await api("tracking/"+tripId+"/locations","POST",{points:batch});if(generation===epoch){const sent=new Set(batch.map(p=>p.pointId));saveQueue(readQueue().filter(p=>!sent.has(p.pointId)));}}
 catch(e){if(generation===epoch){message("Chưa gửi được GPS: "+e.message+". Điểm được giữ trên thiết bị.");if(e.status===401||e.status===403||e.status===409)stopGps();}}
 finally{flushing=false;}
}
function enqueue(point){const q=readQueue();if(q.length>=1000){stopGps();message("Bộ đệm đã đủ 1.000 điểm. Kết nối lại trước khi tiếp tục.");return;}q.push(point);saveQueue(q);void flush();}
function draw(){
 markers.clearLayers();const s=snapshot;if(!s)return;
 if(s.pickup)L.marker([s.pickup.latitude,s.pickup.longitude]).addTo(markers).bindTooltip("Điểm lấy hàng");
 if(s.delivery)L.marker([s.delivery.latitude,s.delivery.longitude]).addTo(markers).bindTooltip("Điểm giao hàng");
 if(s.latest){L.circleMarker([s.latest.latitude,s.latest.longitude],{radius:9,color:s.latest.simulated?"#bb3ea2":"#086e76",fillOpacity:1}).addTo(markers).bindTooltip(s.latest.simulated?"GPS giả lập":"Vị trí xe");
 L.circle([s.latest.latitude,s.latest.longitude],{radius:s.latest.accuracyMeters,color:"#168f94",weight:1,fillOpacity:.08}).addTo(markers);}
 const sorted=[...points.values()].sort((a,b)=>new Date(a.recordedAt)-new Date(b.recordedAt)||a.id-b.id);
 actual.setLatLngs(sorted.map(p=>[p.latitude,p.longitude]));
 updateHistory();
}
function fit(){const coords=[...actual.getLatLngs(),...planned.getLatLngs()];markers.eachLayer(m=>{if(m.getLatLng)coords.push(m.getLatLng());});if(coords.length)map.fitBounds(L.latLngBounds(coords),{padding:[35,35],maxZoom:15});}
function erase(){snapshot=null;points.clear();cursor=0;selectedHistoryId=null;updateHistory();actual.setLatLngs([]);planned.setLatLngs([]);markers.clearLayers();$("driverTools").hidden=true;$("ownerTools").hidden=true;
 $("title").textContent="Chưa chọn chuyến";$("tripStatus").textContent="—";$("lastSeen").textContent="Chưa có dữ liệu GPS.";
 for(const id of ["addresses","routeStatus","queue"])$(id).textContent="";
 $("viewers").replaceChildren();for(const id of ["pickupLat","pickupLng","deliveryLat","deliveryLng","viewerId"])$(id).value="";}
async function refresh(){
 if(!tripId||busy)return;busy=true;const generation=epoch,id=tripId;
 try{
 const s=await api("tracking/"+id);if(generation!==epoch)return;snapshot=s;
 $("title").textContent=s.title;$("tripStatus").textContent=s.status;
 $("addresses").textContent="Lấy: "+s.pickupAddress+"\nGiao: "+s.deliveryAddress;
 $("lastSeen").textContent=s.latest?(s.latest.simulated?"GIẢ LẬP · ":"")+(s.stale?"Mất tín hiệu / dữ liệu cũ · ":"Cập nhật · ")+new Date(s.latest.recordedAt).toLocaleString("vi-VN"):"Chưa có vị trí.";
 $("driverTools").hidden=!s.canSend;$("ownerTools").hidden=!s.canManage;
 $("saveStops").disabled=s.status!=="Assigned";if(!s.canSend)stopGps();
 const history=await api("tracking/"+id+"/locations?afterId="+cursor);if(generation!==epoch)return;
 history.points.forEach(p=>points.set(p.id,p));cursor=history.nextAfterId;$("loadHistory").disabled=!history.hasMore;draw();
 if(s.canSend)void flush();
 }catch(e){if(generation===epoch){message("Không tải được chuyến: "+e.message);if([401,403,404].includes(e.status)){erase();stopGps();epoch++;tripId="";busy=false;}}}
 finally{if(generation===epoch)busy=false;}
}
async function route(){
 const generation=epoch,id=tripId;const r=await api("tracking/"+id+"/route");if(generation!==epoch)return;
 planned.setLatLngs(r.coordinates.map(p=>[p[1],p[0]]));
 $("routeStatus").textContent=r.status==="ReferenceCarRoute"?"Tuyến ô tô tham khảo · "+(r.distanceMeters/1000).toFixed(1)+" km. Chưa kiểm tra hạn chế xe tải.":r.status==="MissingStops"?"Chưa chọn tọa độ lấy/giao hàng.":"Dịch vụ tuyến đường chưa khả dụng; GPS vẫn hoạt động.";fit();
}
async function viewers(){const generation=epoch;const list=await api("tracking/"+tripId+"/participants");if(generation!==epoch)return;$("viewers").replaceChildren();for(const v of list){const li=document.createElement("li"),button=document.createElement("button");li.textContent=v.displayName+" · "+v.userId;button.textContent="Thu hồi";button.className="secondary";button.onclick=()=>run(async()=>{await api("tracking/"+tripId+"/participants/"+v.userId,"DELETE");await viewers();});li.append(button);$("viewers").append(li);}}
async function openTrip(){
 const id=$("tripId").value.trim();if(!/^[0-9a-f-]{36}$/i.test(id))throw new Error("Mã chuyến không hợp lệ");
 stopGps();if(hub){await hub.stop();hub=null;}if(poll)clearInterval(poll);
 epoch++;busy=false;erase();tripId=id;message("");await refresh();if(!snapshot)return;
 const s=snapshot;if(s.pickup){$("pickupLat").value=s.pickup.latitude;$("pickupLng").value=s.pickup.longitude;}if(s.delivery){$("deliveryLat").value=s.delivery.latitude;$("deliveryLng").value=s.delivery.longitude;}
 if(s.canManage)await viewers();await route();
 if(window.signalR){
 hub=new signalR.HubConnectionBuilder().withUrl("/hubs/tracking",{accessTokenFactory:()=>token}).withAutomaticReconnect().build();
 hub.on("Refresh",()=>void refresh());hub.onreconnecting(()=>$("realtime").textContent="Đang kết nối lại");
 hub.onreconnected(async()=>{try{await hub.invoke("JoinTrip",tripId);$("realtime").textContent="Realtime";await refresh();}catch(e){message(e.message);}});
 try{await hub.start();await hub.invoke("JoinTrip",tripId);$("realtime").textContent="Realtime";}catch{$("realtime").textContent="Cập nhật mỗi 15 giây";}
 }
 poll=setInterval(()=>void refresh(),15000);fit();
}
async function loadTrips(){const generation=epoch;const list=await api("tracking/trips?page="+page);if(generation!==epoch)return;
 if(page===1&&!list.length)message("Chưa có chuyến được cấp quyền. Tạo booking hoặc nhờ chủ tin cấp quyền xem.");
 for(const t of list){const o=document.createElement("option");o.value=t.tripId;o.textContent=t.title+" · "+t.status;$("trips").append(o);}$("moreTrips").disabled=list.length<20;page++;}
async function run(work){try{await work();}catch(e){message(e.message);}}
$("login").onsubmit=e=>{e.preventDefault();void run(async()=>{
 const response=await fetch("/api/auth/login",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({phoneNumber:$("phone").value,password:$("password").value})});
 const r=await response.json();if(!response.ok)throw new Error(response.status===401?"Số điện thoại/mật khẩu chưa đúng hoặc tài khoản không được phép đăng nhập. Dùng tài khoản đã đăng ký trên Swagger, số điện thoại dạng +84…; đây không phải tài khoản PostgreSQL.":("Đăng nhập lỗi HTTP "+response.status+". Vui lòng thử lại."));token=r.data.accessToken;userId=r.data.userId;
 $("password").value="";$("identity").textContent=r.data.displayName;$("login").hidden=true;$("session").hidden=false;message("");await loadTrips();
});};
$("logout").onclick=()=>run(()=>endSession());
$("trips").onchange=()=>{if(!$("trips").value)return;$("tripId").value=$("trips").value;void run(openTrip);};
$("reloadTrips").onclick=()=>run(async()=>{page=1;$("trips").length=1;await loadTrips();});
$("historyPosition").oninput=()=>{selectedHistoryId=historyPoints()[Number($("historyPosition").value)]?.id??null;updateHistory(true);};
$("livePosition").onclick=()=>{selectedHistoryId=null;updateHistory();if(snapshot?.latest)map.setView([snapshot.latest.latitude,snapshot.latest.longitude],Math.max(map.getZoom(),15));};
$("openTrip").onclick=()=>run(openTrip);$("moreTrips").onclick=()=>run(loadTrips);$("fit").onclick=fit;$("loadHistory").onclick=()=>run(refresh);
$("startGps").onclick=()=>{if(!navigator.geolocation){message("Thiết bị không hỗ trợ GPS.");return;}stopGps();$("startGps").disabled=true;
 watch=navigator.geolocation.watchPosition(p=>{if(Date.now()-lastSent<10000)return;lastSent=Date.now();enqueue({pointId:crypto.randomUUID(),latitude:p.coords.latitude,longitude:p.coords.longitude,accuracyMeters:p.coords.accuracy,recordedAt:new Date(p.timestamp).toISOString(),simulated:false});},e=>{message("GPS: "+e.message);stopGps();},{enableHighAccuracy:true,maximumAge:0,timeout:20000});};
$("stopGps").onclick=stopGps;
$("clearQueue").onclick=()=>{if(confirm("Xóa các điểm chưa gửi của chuyến này khỏi thiết bị?"))saveQueue([]);};
$("simulate").onclick=()=>run(async()=>{enqueue({pointId:crypto.randomUUID(),latitude:Number($("simLat").value),longitude:Number($("simLng").value),accuracyMeters:5,recordedAt:new Date().toISOString(),simulated:true});});
$("saveStops").onclick=()=>run(async()=>{await api("tracking/"+tripId+"/stops","PUT",{pickup:{latitude:Number($("pickupLat").value),longitude:Number($("pickupLng").value)},delivery:{latitude:Number($("deliveryLat").value),longitude:Number($("deliveryLng").value)}});await refresh();await route();});
$("grant").onclick=()=>run(async()=>{await api("tracking/"+tripId+"/participants/"+$("viewerId").value.trim(),"PUT");await viewers();});
map.on("click",e=>{const target=$("clickTarget").value;if(target){$(target+"Lat").value=e.latlng.lat.toFixed(6);$(target+"Lng").value=e.latlng.lng.toFixed(6);}});
$("clickTarget").onchange=()=>{if($("clickTarget").value)shipmentComposer.stopPicking();};
window.addEventListener("online",()=>void flush());
