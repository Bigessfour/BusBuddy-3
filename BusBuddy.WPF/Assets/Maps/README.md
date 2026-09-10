Map display uses Syncfusion SfMap. Preferred basemap is Google Map Tiles API
(`tile.googleapis.com`) when `GOOGLE_MAPS_API_KEY` is present; OpenStreetMap is the fail-open.

Attribution (Map Tiles API policies):

- `google_maps_on_non_white.png` — outlined Google Maps logo for busy map backgrounds (shown next to copyright when Google tiles are active).
- `google_maps_on_white.png` — non-outlined variant for plain panels (kept for future use).
- Sources: official Maps JS attribution PNGs from `maps.gstatic.com/mapfiles/api-3/images/`.
- Do not modify the logo artwork. Height in UI is 18px (within 16–19dp policy range).

Route polylines are declared in MapView XAML (`RouteTrail` MapPolyline). Clerk map camera:
school GPS → Settings depot → Settings bbox centroid → Lamar/Wiley fail-open (never US-centroid as home view).
