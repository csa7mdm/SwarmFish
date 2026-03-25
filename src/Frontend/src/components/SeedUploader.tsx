"use client"

import { useState, useRef } from 'react';
import { UploadIcon, FileIcon, XIcon, CheckCircleIcon } from 'lucide-react';
import { Progress } from '@/components/ui/progress';
import { Button } from '@/components/ui/button';

export interface UploadedFile {
  name: string;
  size: number;
  type: string;
}

interface SeedUploaderProps {
  onUploadComplete?: (fileInfo: UploadedFile) => void;
}

export function SeedUploader({ onUploadComplete }: SeedUploaderProps) {
  const [dragActive, setDragActive] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [progress, setProgress] = useState(0);
  const [status, setStatus] = useState<'idle' | 'uploading' | 'ingesting' | 'complete'>('idle');
  const inputRef = useRef<HTMLInputElement>(null);

  const handleDrag = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (e.type === 'dragenter' || e.type === 'dragover') {
      setDragActive(true);
    } else if (e.type === 'dragleave') {
      setDragActive(false);
    }
  };

  const processFile = (selectedFile: File) => {
    if (!selectedFile) return;
    
    // Check if valid extension
    const validExtensions = ['.txt', '.pdf', '.md'];
    const lowerName = selectedFile.name.toLowerCase();
    const isValid = validExtensions.some(ext => lowerName.endsWith(ext));
    
    if (!isValid) {
      alert("Invalid file type. Please upload a TXT, PDF, or MD file.");
      return;
    }

    setFile(selectedFile);
    simulateUpload(selectedFile);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(false);
    
    if (e.dataTransfer.files && e.dataTransfer.files[0]) {
      processFile(e.dataTransfer.files[0]);
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    e.preventDefault();
    if (e.target.files && e.target.files[0]) {
      processFile(e.target.files[0]);
    }
  };

  const triggerSelect = () => {
    inputRef.current?.click();
  };

  const simulateUpload = (f: File) => {
    setStatus('uploading');
    setProgress(0);
    
    // Simulate upload progress
    const interval = setInterval(() => {
      setProgress(p => {
        if (p >= 100) {
          clearInterval(interval);
          setStatus('ingesting');
          simulateIngestion(f);
          return 100;
        }
        return p + 10;
      });
    }, 200);
  };

  const simulateIngestion = (f: File) => {
    setProgress(0);
    // Simulate ingestion pipeline progress
    const interval = setInterval(() => {
      setProgress(p => {
        if (p >= 100) {
          clearInterval(interval);
          setStatus('complete');
          if (onUploadComplete) {
            onUploadComplete({
              name: f.name,
              size: f.size,
              type: f.type || 'text/plain'
            });
          }
          return 100;
        }
        return p + 5;
      });
    }, 150);
  };

  const clearFile = () => {
    setFile(null);
    setStatus('idle');
    setProgress(0);
    if (inputRef.current) inputRef.current.value = '';
  };

  if (file && status !== 'idle') {
    return (
      <div className="w-full border border-slate-800 bg-slate-900/40 rounded-xl p-6 relative overflow-hidden">
        {status === 'complete' && (
          <div className="absolute inset-0 bg-green-500/5 pointer-events-none" />
        )}
        
        <div className="flex items-start gap-4">
          <div className={`p-3 rounded-lg flex-shrink-0 ${status === 'complete' ? 'bg-green-500/20 text-green-400' : 'bg-blue-500/20 text-blue-400'}`}>
            {status === 'complete' ? <CheckCircleIcon className="w-6 h-6" /> : <FileIcon className="w-6 h-6" />}
          </div>
          
          <div className="flex-1 min-w-0 pr-8">
            <h4 className="text-sm font-medium text-slate-200 truncate" title={file.name}>
              {file.name}
            </h4>
            
            <div className="text-xs text-slate-500 mb-3">
              {(file.size / 1024 / 1024).toFixed(2)} MB • {
                status === 'uploading' ? 'Uploading to cloud...' :
                status === 'ingesting' ? 'Extracting knowledge graph...' :
                'Document ready'
              }
            </div>
            
            {status !== 'complete' && (
              <div className="space-y-1.5 flex-1">
                <div className="flex justify-between text-xs text-slate-400">
                  <span>{progress}%</span>
                </div>
                <Progress value={progress} className="h-1.5" />
              </div>
            )}
          </div>
          
          <Button 
            variant="ghost" 
            size="icon" 
            className="text-slate-500 hover:text-slate-300 hover:bg-slate-800 shrink-0"
            onClick={clearFile}
          >
            <XIcon className="w-4 h-4" />
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div
      className={`w-full border-2 border-dashed rounded-xl p-10 flex flex-col items-center justify-center transition-all bg-slate-950/20 cursor-pointer ${
        dragActive ? 'border-blue-500 bg-blue-500/5' : 'border-slate-800 hover:border-slate-700 hover:bg-slate-900/40'
      }`}
      onDragEnter={handleDrag}
      onDragLeave={handleDrag}
      onDragOver={handleDrag}
      onDrop={handleDrop}
      onClick={triggerSelect}
    >
      <input
        ref={inputRef}
        type="file"
        className="hidden"
        accept=".txt,.pdf,.md"
        onChange={handleChange}
      />
      <div className="w-16 h-16 bg-slate-900 border border-slate-800 rounded-full flex items-center justify-center mb-4 mt-2">
        <UploadIcon className="w-7 h-7 text-slate-400" />
      </div>
      <h3 className="text-lg font-medium text-slate-200 mb-1">Upload Seed Document</h3>
      <p className="text-sm text-slate-500 text-center max-w-sm mb-6">
        Drag and drop your PDF, TXT, or MD file here, or click to browse. This document will seed the simulation's knowledge graph.
      </p>
      <Button variant="outline" className="bg-slate-900 border-slate-800 hover:bg-slate-800">
        Select File
      </Button>
    </div>
  );
}
