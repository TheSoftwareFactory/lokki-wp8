"""
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
"""
#! coding: utf-8
"""
Script generates loc resources based on customized and default locaalization files.
"""

import sys
import glob
import os
import xml.dom.minidom
import re
import xml.etree.ElementTree as ET
from xml.etree.ElementTree import ElementTree, Element
from os.path import join, basename, dirname, abspath, exists
import codecs
import cgi

THISDIR = dirname(abspath(__file__))

ignored = ["ResourceFlowDirection"]

RESX_FIELD_TEMPLATE = u"""
  <data name="%s" xml:space="preserve">
    <value>%s</value>
  </data>
""".rstrip()
  
def parse(path):
    if exists(path):
        return ET.parse(path)

    return None;

def getText(nodelist):
    rc = []
    for node in nodelist:
        if node.nodeType == node.TEXT_NODE:
            rc.append(node.data)
    return ''.join(rc)

def customize_localizations():
    custom_source_dir_root = join(THISDIR, "../localizations/")
    custom_target_dir_root = join(THISDIR,"../customization/")
    
    default_loc_path = join(THISDIR,"../src/MobileSecurity/Resources/")
    
    customizations = os.listdir(custom_source_dir_root)
    for customization in customizations:
        print "......" + customization
        if not os.path.isdir(os.path.join(custom_source_dir_root, customization)):
            continue
            
        target_dir = join(custom_target_dir_root,customization,"files","Resources")
        source_dir = join(custom_source_dir_root,customization)
        print "Target dir: " + target_dir
        if not os.path.exists(target_dir):
            os.makedirs(target_dir)

        files = [d for d in os.listdir(source_dir) if os.path.splitext(d)[1] == '.resx']
        for filename in files:
            config = []            
            print ""
            print ""
            print "  Found file", filename
            
            custom_loc_file = join(source_dir, filename)
            if custom_loc_file.lower().endswith(".en-us.resx"):
                # Co branding portal generates file with .en-us on the default AppResources.
                # Need to remove it.
                filename = filename.lower().replace("appresources.en-us.resx", "AppResources.resx") 
            
            default_file = join(default_loc_path, filename);

            if not os.path.exists(default_file):
                print "  ERROR! File not found:", default_file
            
            defaultnodes = xml.dom.minidom.parse(default_file).documentElement.childNodes
            for defaultnode in defaultnodes:
                if defaultnode.nodeName == "data":
                    default_string_id = defaultnode.getAttribute("name")
                    default_string_value = ""
                    default_valuenode = defaultnode.getElementsByTagName("value")[0]
                    default_string_value = getText(default_valuenode.childNodes)
                    default_string_value = re.sub('\\\\[uU]([0-9a-fA-F]{4})', '&#x\\1;', default_string_value)
                    default_string_value = cgi.escape(default_string_value)

                    nodes = xml.dom.minidom.parse(custom_loc_file).documentElement.childNodes
                    for node in nodes:
                        if node.nodeName == "data":
                            string_id = node.getAttribute("name")
                            if string_id == default_string_id:
                                print "found same id: " + string_id
                                string_temp = ""
                                valuenode = node.getElementsByTagName("value")[0]
                                string_temp = getText(valuenode.childNodes)
                                string_temp = re.sub('\\\\[uU]([0-9a-fA-F]{4})', '&#x\\1;', string_temp)
                                string_temp = cgi.escape(string_temp)
                                #print "--- NEW VALUE: " + string_id + " ____________ temp: '%s' " % string_temp
                                default_string_value = string_temp
                            
                            else:
                                #print "no match: " + default_string_id
                                continue
                        
                        
                    conf = RESX_FIELD_TEMPLATE % (default_string_id, default_string_value)
                    config.append(conf)

                    
            config = "".join(config)
            print ""
            print "*********************************"
            
            target_file_name = join(target_dir, filename)

            res_template = join(THISDIR, "../build_common/localization/scripts/WP8ResourceTemplate.txt")
            with open(res_template) as f:
                data = f.read()
                config = data % {"FIELD_DATA" : config}
                with codecs.open(target_file_name, 'wb', 'utf-8') as f:
                    f.write(config.replace("\n", "\r\n"))
                print 'Generated', target_file_name
            
    
    
def main():
    from optparse import OptionParser

    parser = OptionParser()
    parser.add_option("-s", "--source",
                      default=join(THISDIR, "..", "src", "MobileSecurity", "Resources", "AppResources.resx" ),
                      help="Path to default RESX")

    (options, args) = parser.parse_args()
    
    customize_localizations()
    

if __name__ == "__main__":
    main()