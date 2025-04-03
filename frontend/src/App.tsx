import React, { useState, useCallback } from 'react';
import { observer } from 'mobx-react-lite';
import { 
  Container, 
  Paper, 
  Typography, 
  Button, 
  Box,
  CircularProgress,
  Alert,
  IconButton,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  List,
  ListItem,
  ListItemText,
  ListItemSecondaryAction,
  Chip,
  useTheme,
  ThemeProvider,
  createTheme,
  Fade,
  Zoom,
  Slider,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Divider,
  Tooltip,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  Switch
} from '@mui/material';
import { 
  CloudUpload as CloudUploadIcon,
  DarkMode as DarkModeIcon,
  LightMode as LightModeIcon,
  Close as CloseIcon,
  ZoomIn as ZoomInIcon,
  History as HistoryIcon,
  Refresh as RefreshIcon,
  Tune as TuneIcon,
  ExpandMore as ExpandMoreIcon,
  Info as InfoIcon,
  Compare as CompareIcon
} from '@mui/icons-material';
import { imageStore } from './stores/ImageStore';
import type { Theme } from '@mui/material/styles';

interface ComparisonHistory {
  id: string;
  image1: string;
  image2: string;
  classification1: string;
  classification2: string;
  similarity: number;
  timestamp: Date;
  metadata: ImageMetadata;
}

interface ImageMetadata {
  width: number;
  height: number;
  size: number;
  type: string;
  lastModified: number;
}

interface ImageFilters {
  brightness: number;
  contrast: number;
  saturation: number;
}

interface ComparisonOptions {
  method: 'basic' | 'histogram' | 'feature';
  threshold: number;
  useFilters: boolean;
}

const App = observer(() => {
  const [isDarkMode, setIsDarkMode] = useState(false);
  const [zoomDialogOpen, setZoomDialogOpen] = useState(false);
  const [selectedImage, setSelectedImage] = useState<string | undefined>(undefined);
  const [historyDialogOpen, setHistoryDialogOpen] = useState(false);
  const [history, setHistory] = useState<ComparisonHistory[]>([]);
  const [dragOver, setDragOver] = useState(false);
  const [filters, setFilters] = useState<ImageFilters>({
    brightness: 100,
    contrast: 100,
    saturation: 100
  });
  const [comparisonOptions, setComparisonOptions] = useState<ComparisonOptions>({
    method: 'basic',
    threshold: 0.8,
    useFilters: false
  });
  const [metadataDialogOpen, setMetadataDialogOpen] = useState(false);
  const [selectedMetadata, setSelectedMetadata] = useState<ImageMetadata | null>(null);
  const [advancedOptionsOpen, setAdvancedOptionsOpen] = useState(false);

  const theme = useTheme();
  const customTheme = createTheme({
    ...theme,
    palette: {
      ...theme.palette,
      mode: isDarkMode ? 'dark' : 'light',
    },
  });

  const handleImage1Upload = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      imageStore.setImage1(file);
      imageStore.classifyImage1();
    }
  };

  const handleImage2Upload = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      imageStore.setImage2(file);
      imageStore.classifyImage2();
    }
  };

  const handleCompare = () => {
    imageStore.compareImages();
  };

  const handleReset = () => {
    imageStore.reset();
  };

  const handleZoom = (imageUrl: string | undefined) => {
    if (imageUrl) {
      setSelectedImage(imageUrl);
      setZoomDialogOpen(true);
    }
  };

  const handleCloseZoom = () => {
    setZoomDialogOpen(false);
    setSelectedImage(undefined);
  };

  const handleHistoryClick = () => {
    setHistoryDialogOpen(true);
  };

  const handleCloseHistory = () => {
    setHistoryDialogOpen(false);
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setDragOver(true);
  };

  const handleDragLeave = () => {
    setDragOver(false);
  };

  const handleDrop = (e: React.DragEvent, imageNumber: 1 | 2) => {
    e.preventDefault();
    setDragOver(false);
    const file = e.dataTransfer.files[0];
    if (file && file.type.startsWith('image/')) {
      if (imageNumber === 1) {
        imageStore.setImage1(file);
        imageStore.classifyImage1();
      } else {
        imageStore.setImage2(file);
        imageStore.classifyImage2();
      }
    }
  };

  const handleRetry = (imageNumber: 1 | 2) => {
    if (imageNumber === 1) {
      imageStore.classifyImage1();
    } else {
      imageStore.classifyImage2();
    }
  };

  const handleFilterChange = (filter: keyof ImageFilters, value: number) => {
    setFilters(prev => ({ ...prev, [filter]: value }));
  };

  const handleComparisonOptionChange = (option: keyof ComparisonOptions, value: any) => {
    setComparisonOptions(prev => ({ ...prev, [option]: value }));
  };

  const handleMetadataClick = (metadata: ImageMetadata | null) => {
    if (metadata) {
      setSelectedMetadata(metadata);
      setMetadataDialogOpen(true);
    }
  };

  const handleCloseMetadata = () => {
    setMetadataDialogOpen(false);
    setSelectedMetadata(null);
  };

  const getImageMetadata = (file: File): ImageMetadata => ({
    width: 0, // Will be set when image loads
    height: 0,
    size: file.size,
    type: file.type,
    lastModified: file.lastModified
  });

  const handleImageLoad = (event: React.SyntheticEvent<HTMLImageElement>, imageNumber: 1 | 2) => {
    const img = event.target as HTMLImageElement;
    const metadata = imageNumber === 1 ? imageStore.image1 : imageStore.image2;
    if (metadata instanceof File) {
      const updatedMetadata = {
        ...getImageMetadata(metadata),
        width: img.naturalWidth,
        height: img.naturalHeight
      };
      if (imageNumber === 1) {
        imageStore.setImage1Metadata(updatedMetadata);
      } else {
        imageStore.setImage2Metadata(updatedMetadata);
      }
    }
  };

  const getImageUrl = (file: File | null): string | undefined => {
    return file ? URL.createObjectURL(file) : undefined;
  };

  return (
    <ThemeProvider theme={customTheme}>
      <Container maxWidth="lg" sx={{ py: 4 }}>
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 4 }}>
          <Typography variant="h4" component="h1">
            Image Classification & Similarity
          </Typography>
          <Box>
            <IconButton onClick={() => setIsDarkMode(!isDarkMode)} color="inherit">
              {isDarkMode ? <LightModeIcon /> : <DarkModeIcon />}
            </IconButton>
            <IconButton onClick={handleHistoryClick} color="inherit">
              <HistoryIcon />
            </IconButton>
          </Box>
        </Box>

        {imageStore.error && (
          <Alert 
            severity="error" 
            sx={{ mb: 2 }}
            action={
              <Button color="inherit" size="small" onClick={() => imageStore.reset()}>
                Retry
              </Button>
            }
          >
            {imageStore.error}
          </Alert>
        )}

        {/* Main Content Area using Box with Flexbox */}
        <Box sx={{ display: 'flex', flexWrap: 'wrap', margin: theme.spacing(-1.5) }}>
          {/* Image 1 */}
          <Box sx={{ padding: theme.spacing(1.5), width: { xs: '100%', md: '50%' } }}>
            <Paper 
              sx={{ 
                p: 2, 
                height: '100%',
                transition: 'all 0.3s ease',
                border: dragOver ? `2px dashed ${theme.palette.primary.main}` : 'none',
                backgroundColor: dragOver ? theme.palette.action.hover : 'inherit'
              }}
              onDragOver={handleDragOver}
              onDragLeave={handleDragLeave}
              onDrop={(e) => handleDrop(e, 1)}
            >
              <Typography variant="h6" gutterBottom>
                Image 1
              </Typography>
              <Box sx={{ mb: 2 }}>
                <input
                  accept="image/*"
                  style={{ display: 'none' }}
                  id="image1-upload"
                  type="file"
                  onChange={handleImage1Upload}
                />
                <label htmlFor="image1-upload">
                  <Button
                    variant="contained"
                    component="span"
                    startIcon={<CloudUploadIcon />}
                    fullWidth
                  >
                    Upload Image 1
                  </Button>
                </label>
              </Box>
              {imageStore.image1 && (
                <Box sx={{ position: 'relative', mb: 2 }}>
                  <Fade in={true} timeout={500}>
                    <img
                      src={getImageUrl(imageStore.image1)}
                      alt="Uploaded 1"
                      style={{ 
                        maxWidth: '100%', 
                        maxHeight: '300px', 
                        objectFit: 'contain',
                        cursor: 'pointer',
                        filter: comparisonOptions.useFilters ? `
                          brightness(${filters.brightness}%)
                          contrast(${filters.contrast}%)
                          saturate(${filters.saturation}%)
                        ` : 'none'
                      }}
                      onClick={() => handleZoom(getImageUrl(imageStore.image1))}
                      onLoad={(e) => handleImageLoad(e, 1)}
                    />
                  </Fade>
                  <Box sx={{ position: 'absolute', top: 8, right: 8, display: 'flex', gap: 1 }}>
                    <Tooltip title="Image Metadata">
                      <IconButton
                        onClick={() => handleMetadataClick(imageStore.image1Metadata)}
                        sx={{
                          backgroundColor: 'rgba(255, 255, 255, 0.8)',
                          '&:hover': { backgroundColor: 'rgba(255, 255, 255, 0.9)' }
                        }}
                      >
                        <InfoIcon />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Zoom">
                      <IconButton
                        onClick={() => handleZoom(getImageUrl(imageStore.image1))}
                        sx={{
                          backgroundColor: 'rgba(255, 255, 255, 0.8)',
                          '&:hover': { backgroundColor: 'rgba(255, 255, 255, 0.9)' }
                        }}
                      >
                        <ZoomInIcon />
                      </IconButton>
                    </Tooltip>
                  </Box>
                </Box>
              )}
              {imageStore.classification1 && (
                <Zoom in={true} timeout={500}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <Typography>
                      Classification: {imageStore.classification1.class}
                    </Typography>
                    <Chip 
                      label={`${Math.round(imageStore.classification1.confidence * 100)}%`}
                      color="primary"
                      size="small"
                    />
                  </Box>
                </Zoom>
              )}
              {imageStore.loading && (
                <Box sx={{ display: 'flex', justifyContent: 'center', mt: 2 }}>
                  <CircularProgress size={24} />
                </Box>
              )}
            </Paper>
          </Box>

          {/* Image 2 */}
          <Box sx={{ padding: theme.spacing(1.5), width: { xs: '100%', md: '50%' } }}>
            <Paper 
              sx={{ 
                p: 2, 
                height: '100%',
                transition: 'all 0.3s ease',
                border: dragOver ? `2px dashed ${theme.palette.primary.main}` : 'none',
                backgroundColor: dragOver ? theme.palette.action.hover : 'inherit'
              }}
              onDragOver={handleDragOver}
              onDragLeave={handleDragLeave}
              onDrop={(e) => handleDrop(e, 2)}
            >
              <Typography variant="h6" gutterBottom>
                Image 2
              </Typography>
              <Box sx={{ mb: 2 }}>
                <input
                  accept="image/*"
                  style={{ display: 'none' }}
                  id="image2-upload"
                  type="file"
                  onChange={handleImage2Upload}
                />
                <label htmlFor="image2-upload">
                  <Button
                    variant="contained"
                    component="span"
                    startIcon={<CloudUploadIcon />}
                    fullWidth
                  >
                    Upload Image 2
                  </Button>
                </label>
              </Box>
              {imageStore.image2 && (
                <Box sx={{ position: 'relative', mb: 2 }}>
                  <Fade in={true} timeout={500}>
                    <img
                      src={getImageUrl(imageStore.image2)}
                      alt="Uploaded 2"
                      style={{ 
                        maxWidth: '100%', 
                        maxHeight: '300px', 
                        objectFit: 'contain',
                        cursor: 'pointer',
                        filter: comparisonOptions.useFilters ? `
                          brightness(${filters.brightness}%)
                          contrast(${filters.contrast}%)
                          saturate(${filters.saturation}%)
                        ` : 'none'
                      }}
                      onClick={() => handleZoom(getImageUrl(imageStore.image2))}
                      onLoad={(e) => handleImageLoad(e, 2)}
                    />
                  </Fade>
                  <Box sx={{ position: 'absolute', top: 8, right: 8, display: 'flex', gap: 1 }}>
                    <Tooltip title="Image Metadata">
                      <IconButton
                        onClick={() => handleMetadataClick(imageStore.image2Metadata)}
                        sx={{
                          backgroundColor: 'rgba(255, 255, 255, 0.8)',
                          '&:hover': { backgroundColor: 'rgba(255, 255, 255, 0.9)' }
                        }}
                      >
                        <InfoIcon />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Zoom">
                      <IconButton
                        onClick={() => handleZoom(getImageUrl(imageStore.image2))}
                        sx={{
                          backgroundColor: 'rgba(255, 255, 255, 0.8)',
                          '&:hover': { backgroundColor: 'rgba(255, 255, 255, 0.9)' }
                        }}
                      >
                        <ZoomInIcon />
                      </IconButton>
                    </Tooltip>
                  </Box>
                </Box>
              )}
              {imageStore.classification2 && (
                <Zoom in={true} timeout={500}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <Typography>
                      Classification: {imageStore.classification2.class}
                    </Typography>
                    <Chip 
                      label={`${Math.round(imageStore.classification2.confidence * 100)}%`}
                      color="primary"
                      size="small"
                    />
                  </Box>
                </Zoom>
              )}
              {imageStore.loading && (
                <Box sx={{ display: 'flex', justifyContent: 'center', mt: 2 }}>
                  <CircularProgress size={24} />
                </Box>
              )}
            </Paper>
          </Box>

          {/* Advanced Options */}
          <Box sx={{ padding: theme.spacing(1.5), width: '100%' }}>
            <Accordion 
              expanded={advancedOptionsOpen}
              onChange={() => setAdvancedOptionsOpen(!advancedOptionsOpen)}
              sx={{ mb: 2 }}
            >
              <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  <TuneIcon />
                  <Typography>Advanced Options</Typography>
                </Box>
              </AccordionSummary>
              <AccordionDetails>
                {/* Inner layout for Advanced Options */}
                <Box sx={{ display: 'flex', flexWrap: 'wrap', margin: theme.spacing(-1.5) }}>
                  <Box sx={{ padding: theme.spacing(1.5), width: { xs: '100%', md: '50%' } }}>
                    <Typography variant="subtitle1" gutterBottom>
                      Comparison Method
                    </Typography>
                    <FormControl fullWidth>
                      <InputLabel>Method</InputLabel>
                      <Select
                        value={comparisonOptions.method}
                        label="Method"
                        onChange={(e) => handleComparisonOptionChange('method', e.target.value)}
                      >
                        <MenuItem value="basic">Basic Similarity</MenuItem>
                        <MenuItem value="histogram">Histogram Comparison</MenuItem>
                        <MenuItem value="feature">Feature Matching</MenuItem>
                      </Select>
                    </FormControl>
                  </Box>
                  <Box sx={{ padding: theme.spacing(1.5), width: { xs: '100%', md: '50%' } }}>
                    <Typography variant="subtitle1" gutterBottom>
                      Similarity Threshold
                    </Typography>
                    <Slider
                      value={comparisonOptions.threshold}
                      onChange={(_, value) => handleComparisonOptionChange('threshold', value)}
                      min={0}
                      max={1}
                      step={0.1}
                      marks
                    />
                  </Box>
                  <Box sx={{ padding: theme.spacing(1.5), width: '100%' }}>
                    <Typography variant="subtitle1" gutterBottom>
                      Image Filters
                    </Typography>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 2 }}>
                      <Typography>Apply Filters</Typography>
                      <Switch
                        checked={comparisonOptions.useFilters}
                        onChange={(e) => handleComparisonOptionChange('useFilters', e.target.checked)}
                      />
                    </Box>
                    {/* Inner layout for Filters */}
                    <Box sx={{ display: 'flex', flexWrap: 'wrap', margin: theme.spacing(-1), width: '100%', padding: theme.spacing(1.5) }}> {/* Added width and adjusted padding/margin for nesting */}
                      <Box sx={{ padding: theme.spacing(1), width: '100%' }}>
                        <Typography gutterBottom>Brightness</Typography>
                        <Slider
                          value={filters.brightness}
                          onChange={(_, value) => handleFilterChange('brightness', value)}
                          min={0}
                          max={200}
                          step={1}
                          marks
                        />
                      </Box>
                      <Box sx={{ padding: theme.spacing(1), width: '100%' }}>
                        <Typography gutterBottom>Contrast</Typography>
                        <Slider
                          value={filters.contrast}
                          onChange={(_, value) => handleFilterChange('contrast', value)}
                          min={0}
                          max={200}
                          step={1}
                          marks
                        />
                      </Box>
                      <Box sx={{ padding: theme.spacing(1), width: '100%' }}>
                        <Typography gutterBottom>Saturation</Typography>
                        <Slider
                          value={filters.saturation}
                          onChange={(_, value) => handleFilterChange('saturation', value)}
                          min={0}
                          max={200}
                          step={1}
                          marks
                        />
                      </Box>
                    </Box>
                  </Box>
                </Box>
              </AccordionDetails>
            </Accordion>
          </Box>

          {/* Controls */}
          <Box sx={{ padding: theme.spacing(1.5), width: '100%' }}>
            <Box sx={{ display: 'flex', gap: 2, justifyContent: 'center' }}>
              <Button
                variant="contained"
                color="primary"
                onClick={handleCompare}
                disabled={!imageStore.image1 || !imageStore.image2 || imageStore.loading}
                startIcon={<RefreshIcon />}
              >
                Compare Images
              </Button>
              <Button
                variant="outlined"
                color="secondary"
                onClick={handleReset}
                disabled={imageStore.loading}
                startIcon={<CloseIcon />}
              >
                Reset
              </Button>
            </Box>
          </Box>

          {/* Similarity Result */}
          {imageStore.similarity && (
            <Box sx={{ padding: theme.spacing(1.5), width: '100%' }}>
              <Zoom in={true} timeout={500}>
                <Paper sx={{ p: 2, textAlign: 'center' }}>
                  <Typography variant="h6">
                    Similarity Score: {Math.round(imageStore.similarity.score * 100)}%
                  </Typography>
                </Paper>
              </Zoom>
            </Box>
          )}
        </Box>

        {/* Zoom Dialog */}
        <Dialog
          open={zoomDialogOpen}
          onClose={handleCloseZoom}
          maxWidth="md"
          fullWidth
        >
          <DialogTitle>
            Image Preview
            <IconButton
              aria-label="close"
              onClick={handleCloseZoom}
              sx={{
                position: 'absolute',
                right: 8,
                top: 8,
              }}
            >
              <CloseIcon />
            </IconButton>
          </DialogTitle>
          <DialogContent>
            {selectedImage && (
              <img
                src={selectedImage}
                alt="Preview"
                style={{ width: '100%', height: 'auto' }}
              />
            )}
          </DialogContent>
        </Dialog>

        {/* History Dialog */}
        <Dialog
          open={historyDialogOpen}
          onClose={handleCloseHistory}
          maxWidth="md"
          fullWidth
        >
          <DialogTitle>
            Comparison History
            <IconButton
              aria-label="close"
              onClick={handleCloseHistory}
              sx={{
                position: 'absolute',
                right: 8,
                top: 8,
              }}
            >
              <CloseIcon />
            </IconButton>
          </DialogTitle>
          <DialogContent>
            <List>
              {history.map((item) => (
                <ListItem key={item.id}>
                  <ListItemText
                    primary={`Comparison ${new Date(item.timestamp).toLocaleString()}`}
                    secondary={
                      <>
                        <Typography component="span" variant="body2">
                          Image 1: {item.classification1}
                        </Typography>
                        <br />
                        <Typography component="span" variant="body2">
                          Image 2: {item.classification2}
                        </Typography>
                      </>
                    }
                  />
                  <ListItemSecondaryAction>
                    <Chip
                      label={`${Math.round(item.similarity * 100)}%`}
                      color="primary"
                    />
                  </ListItemSecondaryAction>
                </ListItem>
              ))}
            </List>
          </DialogContent>
          <DialogActions>
            <Button onClick={handleCloseHistory}>Close</Button>
          </DialogActions>
        </Dialog>

        {/* Metadata Dialog */}
        <Dialog
          open={metadataDialogOpen}
          onClose={handleCloseMetadata}
          maxWidth="sm"
          fullWidth
        >
          <DialogTitle>
            Image Metadata
            <IconButton
              aria-label="close"
              onClick={handleCloseMetadata}
              sx={{
                position: 'absolute',
                right: 8,
                top: 8,
              }}
            >
              <CloseIcon />
            </IconButton>
          </DialogTitle>
          <DialogContent>
            {selectedMetadata && (
              <List>
                <ListItem>
                  <ListItemText
                    primary="Dimensions"
                    secondary={`${selectedMetadata.width} x ${selectedMetadata.height} pixels`}
                  />
                </ListItem>
                <ListItem>
                  <ListItemText
                    primary="File Size"
                    secondary={`${(selectedMetadata.size / 1024).toFixed(2)} KB`}
                  />
                </ListItem>
                <ListItem>
                  <ListItemText
                    primary="File Type"
                    secondary={selectedMetadata.type}
                  />
                </ListItem>
                <ListItem>
                  <ListItemText
                    primary="Last Modified"
                    secondary={new Date(selectedMetadata.lastModified).toLocaleString()}
                  />
                </ListItem>
              </List>
            )}
          </DialogContent>
          <DialogActions>
            <Button onClick={handleCloseMetadata}>Close</Button>
          </DialogActions>
        </Dialog>
      </Container>
    </ThemeProvider>
  );
});

export default App;
