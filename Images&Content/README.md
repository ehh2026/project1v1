# Content Folder

This folder contains the world map image and location-specific content for the Interactive World Map application.

## Required Files

- **world_map.png** (or World Map 1976.jpg): The main world map image displayed in full-screen
- **locations.json**: Configuration file defining all interactive locations

## locations.json Format

```json
{
  "locations": [
    {
      "id": "unique_id",
      "name": "Location Name",
      "latitude": 40.7128,
      "longitude": -74.0060,
      "contentFile": "content_image.jpg",
      "contentType": "image"
    }
  ]
}
```

## Supported Content Types

- **Images**: .png, .jpg, .jpeg, .bmp
- **Text**: .txt files

## Coordinate System

- **Latitude**: -90 (South Pole) to +90 (North Pole)
- **Longitude**: -180 (West) to +180 (East)
