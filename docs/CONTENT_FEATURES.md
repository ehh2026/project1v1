# Content Display Features

## Image Ordering

Images in location content folders are now automatically sorted by filename. To control the display order in the thumbnail browser, prefix your image filenames with numbers:

```
1-first-image.jpg
2-second-image.jpg
3-third-image.jpg
```

The images will be displayed in alphabetical order by filename, so numeric prefixes ensure the correct sequence.

## Didactic Text Window

When a location's content folder contains a file named `didactic.txt`, a didactic information window will automatically appear to the left of the main content window.

### Setup

1. Create a text file named `didactic.txt` in the location's content folder
2. Add informational text about the location or content
3. The window will automatically appear when the location is selected

### Example Structure

```
Images&Content/
  Kevin/
    1-letter-product-pic.jpg
    2-another-image.jpg
    3-final-image.jpg
    didactic.txt
```

### Features

- Automatically positioned to the left of the content window
- Scrollable for longer text content
- Matches the height of the content window
- Closes automatically when the content window closes
- Animated fade-in/fade-out transitions

## Window Layout

When all features are active, the windows are arranged as follows:

```
[Didactic Window] [Content Window] [Thumbnail Browser]
     (left)          (center)            (right)
```

The didactic window provides contextual information while the user browses through images using the thumbnail browser.
