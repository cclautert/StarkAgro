import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  // Prerender only non-parameterized routes to avoid needing getPrerenderParams.
  { path: 'login', renderMode: RenderMode.Prerender },
  { path: 'login/callback', renderMode: RenderMode.Prerender },
  { path: 'home', renderMode: RenderMode.Prerender },
  // Fallback: render other routes on-demand at runtime
  { path: '**', renderMode: RenderMode.Server }
];
