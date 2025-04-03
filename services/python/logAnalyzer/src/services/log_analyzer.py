import re
import pandas as pd
import numpy as np
from datetime import datetime, timedelta
from typing import List, Dict, Any, Optional
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import cosine_similarity
import json
import os
from pathlib import Path

class LogAnalyzer:
    def __init__(self, error_patterns: List[str]):
        self.error_patterns = error_patterns
        self.vectorizer = TfidfVectorizer(
            max_features=1000,
            stop_words='english',
            ngram_range=(1, 2)
        )
        
    def parse_log_file(self, log_content: str) -> pd.DataFrame:
        """Parse log content into a structured DataFrame."""
        # Basic log parsing (can be extended based on log format)
        lines = log_content.split('\n')
        parsed_logs = []
        
        for line in lines:
            if not line.strip():
                continue
                
            # Try to extract timestamp and message
            timestamp_match = re.match(r'(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})', line)
            if timestamp_match:
                timestamp = timestamp_match.group(1)
                message = line[len(timestamp):].strip()
            else:
                timestamp = None
                message = line
                
            parsed_logs.append({
                'timestamp': timestamp,
                'message': message,
                'is_error': any(re.search(pattern, message, re.IGNORECASE) for pattern in self.error_patterns)
            })
            
        return pd.DataFrame(parsed_logs)
    
    def analyze_errors(self, df: pd.DataFrame) -> Dict[str, Any]:
        """Analyze error patterns and generate insights."""
        error_df = df[df['is_error']].copy()
        
        if error_df.empty:
            return {
                'status': 'success',
                'message': 'No errors found in the logs',
                'error_count': 0
            }
            
        # Calculate error statistics
        error_count = len(error_df)
        error_frequency = error_df.groupby('message').size().sort_values(ascending=False)
        
        # Find similar error messages
        error_messages = error_df['message'].tolist()
        if error_messages:
            tfidf_matrix = self.vectorizer.fit_transform(error_messages)
            similarity_matrix = cosine_similarity(tfidf_matrix)
            
            # Group similar errors
            error_groups = []
            processed_indices = set()
            
            for i in range(len(error_messages)):
                if i in processed_indices:
                    continue
                    
                similar_indices = np.where(similarity_matrix[i] > 0.8)[0]
                if len(similar_indices) > 1:
                    error_groups.append({
                        'pattern': error_messages[i],
                        'count': len(similar_indices),
                        'similar_messages': [error_messages[j] for j in similar_indices if j != i]
                    })
                    processed_indices.update(similar_indices)
        
        # Generate recommendations
        recommendations = self._generate_recommendations(error_df, error_groups)
        
        return {
            'status': 'success',
            'error_count': error_count,
            'error_frequency': error_frequency.to_dict(),
            'error_groups': error_groups,
            'recommendations': recommendations
        }
    
    def _generate_recommendations(self, error_df: pd.DataFrame, error_groups: List[Dict]) -> List[Dict]:
        """Generate recommendations based on error patterns."""
        recommendations = []
        
        # Analyze error frequency
        for group in error_groups:
            if group['count'] >= 3:
                recommendations.append({
                    'type': 'frequent_error',
                    'message': f"Frequent error pattern detected: {group['pattern']}",
                    'occurrences': group['count'],
                    'suggestion': "Consider implementing error handling or monitoring for this pattern"
                })
        
        # Analyze error timing
        if 'timestamp' in error_df.columns and not error_df['timestamp'].isna().all():
            error_df['timestamp'] = pd.to_datetime(error_df['timestamp'])
            time_based_analysis = error_df.groupby(error_df['timestamp'].dt.hour).size()
            
            if time_based_analysis.max() > 10:
                peak_hour = time_based_analysis.idxmax()
                recommendations.append({
                    'type': 'time_pattern',
                    'message': f"High error frequency detected during hour {peak_hour}",
                    'occurrences': int(time_based_analysis.max()),
                    'suggestion': "Consider investigating system load or scheduled tasks during this time"
                })
        
        return recommendations
    
    def save_analysis_results(self, results: Dict[str, Any], output_dir: str) -> str:
        """Save analysis results to a JSON file."""
        os.makedirs(output_dir, exist_ok=True)
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_file = os.path.join(output_dir, f"analysis_{timestamp}.json")
        
        with open(output_file, 'w') as f:
            json.dump(results, f, indent=2)
            
        return output_file 