import { makeAutoObservable } from 'mobx';

export interface Classification {
  class: string;
  confidence: number;
}

export interface Similarity {
  score: number;
  details: {
    structural: number;
    perceptual: number;
    color: number;
  };
}

export interface ImageMetadata {
  width: number;
  height: number;
  size: number;
  type: string;
  lastModified: number;
}

export interface ComparisonOptions {
  method: 'structural' | 'perceptual' | 'color' | 'combined';
  threshold: number;
  useFilters: boolean;
}

class ImageStore {
  image1: File | null = null;
  image2: File | null = null;
  classification1: Classification | null = null;
  classification2: Classification | null = null;
  similarity: Similarity | null = null;
  error: string | null = null;
  loading = false;
  image1Metadata: ImageMetadata | null = null;
  image2Metadata: ImageMetadata | null = null;
  comparisonOptions: ComparisonOptions = {
    method: 'combined',
    threshold: 0.8,
    useFilters: false
  };

  constructor() {
    makeAutoObservable(this);
  }

  setImage1(file: File | null) {
    this.image1 = file;
    this.classification1 = null;
    this.similarity = null;
    this.error = null;
  }

  setImage2(file: File | null) {
    this.image2 = file;
    this.classification2 = null;
    this.similarity = null;
    this.error = null;
  }

  setImage1Metadata(metadata: ImageMetadata | null) {
    this.image1Metadata = metadata;
  }

  setImage2Metadata(metadata: ImageMetadata | null) {
    this.image2Metadata = metadata;
  }

  setComparisonOptions(options: Partial<ComparisonOptions>) {
    this.comparisonOptions = {
      ...this.comparisonOptions,
      ...options
    };
  }

  async classifyImage1() {
    if (!this.image1) return;
    
    this.loading = true;
    this.error = null;
    
    try {
      const formData = new FormData();
      formData.append('image', this.image1);
      
      const response = await fetch('/api/classify', {
        method: 'POST',
        body: formData
      });
      
      if (!response.ok) {
        throw new Error('Classification failed');
      }
      
      const data = await response.json();
      this.classification1 = data;
    } catch (err) {
      this.error = err instanceof Error ? err.message : 'Classification failed';
    } finally {
      this.loading = false;
    }
  }

  async classifyImage2() {
    if (!this.image2) return;
    
    this.loading = true;
    this.error = null;
    
    try {
      const formData = new FormData();
      formData.append('image', this.image2);
      
      const response = await fetch('/api/classify', {
        method: 'POST',
        body: formData
      });
      
      if (!response.ok) {
        throw new Error('Classification failed');
      }
      
      const data = await response.json();
      this.classification2 = data;
    } catch (err) {
      this.error = err instanceof Error ? err.message : 'Classification failed';
    } finally {
      this.loading = false;
    }
  }

  async compareImages() {
    if (!this.image1 || !this.image2) return;
    
    this.loading = true;
    this.error = null;
    
    try {
      const formData = new FormData();
      formData.append('image1', this.image1);
      formData.append('image2', this.image2);
      formData.append('options', JSON.stringify(this.comparisonOptions));
      
      const response = await fetch('/api/compare', {
        method: 'POST',
        body: formData
      });
      
      if (!response.ok) {
        throw new Error('Comparison failed');
      }
      
      const data = await response.json();
      this.similarity = data;
    } catch (err) {
      this.error = err instanceof Error ? err.message : 'Comparison failed';
    } finally {
      this.loading = false;
    }
  }

  reset() {
    this.image1 = null;
    this.image2 = null;
    this.classification1 = null;
    this.classification2 = null;
    this.similarity = null;
    this.error = null;
    this.loading = false;
    this.image1Metadata = null;
    this.image2Metadata = null;
    this.comparisonOptions = {
      method: 'combined',
      threshold: 0.8,
      useFilters: false
    };
  }
}

export const imageStore = new ImageStore(); 