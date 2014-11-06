"""
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
"""
"""
Tool for converting RESX to our localization XML format
"""

import xml.etree.ElementTree as ET
from xml.etree.ElementTree import ElementTree, Element
from os.path import join, basename, dirname, abspath, exists

def indent(elem, level=0, indention = "    "):
    i = "\n" + level * indention

    if len(elem):
        if not elem.text or not elem.text.strip():
            elem.text = i + indention
        if not elem.tail or not elem.tail.strip():
            elem.tail = i
        for elem in elem:
            indent(elem, level+1)
        if not elem.tail or not elem.tail.strip():
            elem.tail = i
    else:
        if level and (not elem.tail or not elem.tail.strip()):
            elem.tail = i

def parse(path):    
    if exists(path):        
        return ET.parse(path)

    return None;

def insert_or_update(locroot, labelid, value):
    e = ET.SubElement(locroot, 'label')
    e.set("id", name)
    e.text  = value

def process(options):

    resx = parse(options.source)
    locxml = parse(options.target)

    labels = {} 

    # First populate with values from the localization file
    if locxml is not None:
        for datatag in locxml.findall("label"):
        
            name = datatag.get("id")
            value = datatag.text

            labels[name] = value            

    locxml = ElementTree()        
    locroot =  Element("xml")
 
    locxml._setroot( locroot )

    # TODO: Update RESX with the fields in loc file
    
    # Update the loc file with new fields from RESX
    # To detect removed fields
    resx_labels = []
    resourcelang = None

    ignored = ["ResourceFlowDirection"]

    for datatag in resx.findall("data"):
        
        name = datatag.get("name")

        if name == "ResourceLanguage":
            for t in datatag.getiterator("value"):
                resourcelang = t.text
                break
        elif name not in ignored: # Assuming the rest are localizations
            for t in datatag.getiterator("value"):
                value = t.text

                labels[name] = value
                resx_labels.append(name)
                break

    keys = labels.keys(); keys.sort()
    for name in keys:

        # Remove unused labels from localization
        if name not in resx_labels:
            print "Obsolete: '%s'(%s) " % (name, labels[name])
            continue

        value =  labels[name]
        e = ET.SubElement(locroot, 'label')
        e.set("id", name)
        e.text  = value 
 
    #import pdb;pdb.set_trace()

    # Prettify
    indent(locroot)

    print
    ET.dump(locxml)
    
    locxml.write(options.target, 
        xml_declaration = True,
        encoding = "utf-8")
    

def main():
    from optparse import OptionParser
 
    parser = OptionParser()
    parser.add_option("-s", "--source",                      
                      help="Path to source RESX file")
    parser.add_option("-t", "--target",                      
                      help="Path to generated localization XML")

    (options, args) = parser.parse_args()

    if None in [options.source, options.target]:
        print "Target and source must be set"
        return -1

    process(options)
    
if __name__ == "__main__":
    main()    