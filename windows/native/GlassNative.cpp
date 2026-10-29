#define NOMINMAX
#include <windows.h>
#include <dwmapi.h>
#include <DispatcherQueue.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.Foundation.Numerics.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Composition.Desktop.h>
#include <windows.ui.composition.interop.h>
#include <vector>
#include <memory>
using namespace winrt;
using namespace Windows::UI::Composition;
using namespace Windows::UI::Composition::Desktop;
struct Shape { float width,height,radius,opacity,m11,m12,m21,m22,x,y; };
struct Layer { SpriteVisual visual{nullptr}; CompositionRoundedRectangleGeometry geometry{nullptr}; };
struct Context {
 Windows::System::DispatcherQueueController queue{nullptr};
 Compositor compositor{nullptr};
 DesktopWindowTarget target{nullptr};
 ContainerVisual root{nullptr};
 CompositionBackdropBrush backdrop{nullptr};
 std::vector<Layer> layers;
};
extern "C" __declspec(dllexport) HRESULT __cdecl GlassCreate(HWND hwnd, void** result) noexcept {
 *result=nullptr;
 try {
  auto ctx=std::make_unique<Context>();
  if(!Windows::System::DispatcherQueue::GetForCurrentThread()){
   DispatcherQueueOptions options{sizeof(DispatcherQueueOptions),DQTYPE_THREAD_CURRENT,DQTAT_COM_STA};
   check_hresult(CreateDispatcherQueueController(options,reinterpret_cast<ABI::Windows::System::IDispatcherQueueController**>(put_abi(ctx->queue))));
  }
  BOOL enabled=TRUE;check_hresult(DwmSetWindowAttribute(hwnd,DWMWA_USE_HOSTBACKDROPBRUSH,&enabled,sizeof(enabled)));
  ctx->compositor=Compositor();
  auto interop=ctx->compositor.as<ABI::Windows::UI::Composition::Desktop::ICompositorDesktopInterop>();
  check_hresult(interop->CreateDesktopWindowTarget(hwnd,TRUE,reinterpret_cast<ABI::Windows::UI::Composition::Desktop::IDesktopWindowTarget**>(put_abi(ctx->target))));
  ctx->root=ctx->compositor.CreateContainerVisual();ctx->root.RelativeSizeAdjustment({1,1});ctx->target.Root(ctx->root);
  ctx->backdrop=ctx->compositor.CreateHostBackdropBrush();
  *result=ctx.release();return S_OK;
 }catch(...){return to_hresult();}
}
extern "C" __declspec(dllexport) HRESULT __cdecl GlassUpdate(void* raw,const Shape* shapes,int count) noexcept {
 if(!raw||count<0||count>1024)return E_INVALIDARG;
 try{
  auto& ctx=*static_cast<Context*>(raw);
  while(static_cast<int>(ctx.layers.size())<count){
   Layer layer;layer.visual=ctx.compositor.CreateSpriteVisual();layer.geometry=ctx.compositor.CreateRoundedRectangleGeometry();
   layer.visual.Brush(ctx.backdrop);layer.visual.Clip(ctx.compositor.CreateGeometricClip(layer.geometry));ctx.root.Children().InsertAtTop(layer.visual);ctx.layers.push_back(layer);
  }
  for(int i=0;i<static_cast<int>(ctx.layers.size());i++){
   auto& l=ctx.layers[i];if(i>=count){l.visual.Opacity(0);continue;}
   auto const& s=shapes[i];l.visual.Size({s.width,s.height});l.geometry.Size({s.width,s.height});l.geometry.CornerRadius({s.radius,s.radius});
   l.visual.TransformMatrix({s.m11,s.m12,0,0,s.m21,s.m22,0,0,0,0,1,0,s.x,s.y,0,1});l.visual.Opacity(s.opacity);
  }
  return S_OK;
 }catch(...){return to_hresult();}
}
extern "C" __declspec(dllexport) void __cdecl GlassDestroy(void* raw) noexcept {
 if(!raw)return;auto ctx=static_cast<Context*>(raw);
 try{
  ctx->target.Root(nullptr);
  ctx->root.Children().RemoveAll();
  ctx->layers.clear();
  ctx->backdrop.Close();ctx->backdrop=nullptr;
  ctx->root.Close();ctx->root=nullptr;
  ctx->target.Close();ctx->target=nullptr;
  ctx->compositor.Close();ctx->compositor=nullptr;
 }catch(...){}
 if(ctx->queue){
  try{
   auto shutdown=ctx->queue.ShutdownQueueAsync();
   auto until=GetTickCount64()+2000;
   while(shutdown.Status()==Windows::Foundation::AsyncStatus::Started&&GetTickCount64()<until){
    MSG msg;while(PeekMessage(&msg,nullptr,0,0,PM_REMOVE)){if(msg.message==WM_QUIT){PostQuitMessage(static_cast<int>(msg.wParam));break;}TranslateMessage(&msg);DispatchMessage(&msg);}
    MsgWaitForMultipleObjectsEx(0,nullptr,10,QS_ALLINPUT,MWMO_INPUTAVAILABLE);
   }
  }catch(...){}
 }
 delete ctx;
}
