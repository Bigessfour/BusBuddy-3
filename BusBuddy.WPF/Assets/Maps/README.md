Map assets for overlays (Shapefiles).

Expected directories and files:

- Assets/Maps/WileyDistrict/
  - WileyDistrict.shp
  - WileyDistrict.dbf
  - WileyDistrict.shx
  - WileyDistrict.prj

- Assets/Maps/WileyTown/
  - WileyTown.shp
  - WileyTown.dbf
  - WileyTown.shx
  - WileyTown.prj

Notes:
- These files are copied to output via csproj <None Include ... CopyToOutputDirectory> so ShapeFileLayer.Uri can use relative paths.
- Ensure coordinate system is WGS84 (EPSG:4326) or include proper .prj; SfMap projects shape coordinates per documentation.
- Data source guidance: use official public shapefiles (county/town boundaries) that permit redistribution.
