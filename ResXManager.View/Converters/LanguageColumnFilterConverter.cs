﻿namespace tomenglertde.ResXManager.View.Converters
{
    using System;
    using System.Globalization;
    using System.Windows.Controls;
    using System.Windows.Data;
    using tomenglertde.ResXManager.View.ColumnHeaders;

    public class LanguageColumnFilterConverter : IValueConverter
    {
        public LanguageColumnFilterConverter()
        {
                
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var collectionViewSource = new CollectionViewSource() { Source = value };
            var collectionView = collectionViewSource.View;
            collectionView.Filter = Filter;
            return collectionView;
        }

        static bool Filter(object item)
        {
            if (item == null)
                return false;

            return ((DataGridColumn)item).Header is ILanguageColumnHeader;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}