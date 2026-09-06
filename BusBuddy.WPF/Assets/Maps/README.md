Map display uses Syncfusion SfMap with OpenStreetMap tiles. District or town shapefiles are not used.

Route polylines are declared in MapView XAML (`RouteTrail` MapPolyline on `RouteTrailLayer` under the OSM ImageryLayer). `MapRouteTrail` builds line vs stop pins; `MapRouteTrailLayer` mutates the polyline on the UI thread and replays after Loaded.

The gold line is the stored road path (decoded `encodedPolyline`, else stop-to-stop segments). Stop pins are the stop list, not every road vertex. Selecting a route draws stored geometry; Google Routes runs on Map **Refresh** or Route Management **Drive Path**.

Clerk map camera: first school destination with GPS, then Settings bus-barn coordinates, then Settings bounding-box centroid. With none of those set, the map uses a US overview until the clerk configures the district.
