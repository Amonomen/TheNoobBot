﻿using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace nManager.Helpful
{
    /// <summary>
    /// XmlSerializer
    /// </summary>
    public static class XmlSerializer
    {
        /// <summary>
        /// Serializes the specified Class to XML.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="object">The @object.</param>
        /// <returns></returns>
        public static Boolean Serialize(String path, object @object)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
            FileStream fs = null;
            try
            {
                using (fs = new FileStream(path, FileMode.Create))
                {
                    using (StreamWriter w = new StreamWriter(fs, Encoding.UTF8))
                    {
                        if (@object.GetType().ToString() == "Quester.Profile.QuesterProfile")
                        {
                            // create a XmlAttributes class with empty namespace and namespace disabled
                            var xmlAttributes = new XmlAttributes {XmlType = new XmlTypeAttribute {Namespace = ""}, Xmlns = false};
                            // create a XmlAttributeOverrides class
                            var xmlAttributeOverrides = new XmlAttributeOverrides();
                            // implement our previously created XmlAttributes to the overrider for our specificed class
                            xmlAttributeOverrides.Add(@object.GetType(), xmlAttributes);
                            // initialize the serializer for our class and attribute override
                            var s = new System.Xml.Serialization.XmlSerializer(@object.GetType(), xmlAttributeOverrides);
                            // create a blank XmlSerializerNamespaces
                            var xmlSrzNamespace = new XmlSerializerNamespaces();
                            xmlSrzNamespace.Add("", "");
                            // Serialize with blank XmlSerializerNames using our initialized serializer with namespace disabled
                            s.Serialize(w, @object, xmlSrzNamespace);
                            // All kind of namespace are totally unable to serialize.
                        }
                        else
                        {
                            var s = new System.Xml.Serialization.XmlSerializer(@object.GetType());
                            s.Serialize(w, @object);
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    if (fs != null)
                        fs.Close();
                }
                catch
                {
                }
                Logging.WriteError("Serialize(String path, object @object)#2: " + ex);
                MessageBox.Show("XML Serialize: " + ex);
            }

            return false;
        }

        /// <summary>
        /// Deserializes the XML to Class.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public static T Deserialize<T>(String path)
        {
            if (!File.Exists(path))
                return default(T);

            FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            System.Xml.Serialization.XmlSerializer s = new System.Xml.Serialization.XmlSerializer(typeof (T));
            T result = (T) s.Deserialize(fs);
            fs.Close();
            return result;
        }
    }
}